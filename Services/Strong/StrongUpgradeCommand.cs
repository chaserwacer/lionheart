using lionheart.Data;
using lionheart.Functional;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace lionheart.Services.Strong;

/// <summary>
/// The command line the set-kind upgrade runs behind:
/// <c>dotnet run -- upgrade-strong-set-kinds &lt;export.csv&gt; --user &lt;athlete-id&gt;</c>.
/// </summary>
/// <remarks>
/// Given the same export the sessions were imported from, read in the same time zone, as
/// <see cref="StrongImportCommand"/> is. Safe to run again: a session already upgraded reads as
/// current and is left as it is.
/// </remarks>
internal static class StrongUpgradeCommand
{
    public const string Verb = "upgrade-strong-set-kinds";

    private const string Usage =
        "usage: dotnet run -- upgrade-strong-set-kinds <strong-export.csv> --user <athlete-guid>";

    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (args is not [Verb, var export, "--user", var user] || !Guid.TryParse(user, out var userId))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        using var host = Host.CreateApplicationBuilder().AddLionheartDatabase().Build();
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ModelContext>();

        var upgraded = await StrongMovementMap.Shipped()
            .BindAsync(map => new StrongSetKindUpgrader(context, map, TimeZoneInfo.Local, TimeProvider.System)
                .UpgradeAsync(export, userId, ct));

        return upgraded.Match(Describe, Refuse);
    }

    private static int Describe(StrongUpgradeReport report)
    {
        Console.WriteLine($"Times read in {TimeZoneInfo.Local.Id}.");
        Console.WriteLine(
            $"{report.WorkoutsInExport} workouts in the export, {report.NotImported} not imported (left to import-strong).");
        Console.WriteLine(
            $"{report.Upgraded} upgraded to set kinds, {report.AlreadyCurrent} already current, "
            + $"{report.LeftAlone.Count} left alone as changed since import.");

        var sets = report.UpgradedSets;
        if (report.Upgraded > 0)
        {
            Console.WriteLine(
                $"  upgraded sets: {sets.WarmUps} warm-ups, {sets.Drops} drop sets, {sets.Failures} to failure, "
                + $"{sets.Working} working.");
        }

        foreach (var workout in report.LeftAlone)
        {
            Console.WriteLine($"  left alone: {workout.Start:yyyy-MM-dd HH:mm} {workout.Name}");
        }

        return 0;
    }

    private static int Refuse(Problem problem)
    {
        Console.Error.WriteLine($"upgrade refused: {problem.Description}");
        return 1;
    }
}
