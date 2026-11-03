using lionheart.Data;
using lionheart.Functional;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace lionheart.Services.Training.PersonalRecords;

/// <summary>
/// The command line the record rebuild runs behind:
/// <c>dotnet run -- rebuild-records --user &lt;athlete-id&gt;</c>.
/// </summary>
/// <remarks>
/// For after anything that changes history out of order or changes how sets are read, packaged
/// like the imports: a command, not an endpoint, building a host with configuration and the
/// database only. The connection string is relative, so it rebuilds the database of the checkout
/// it runs from.
/// </remarks>
internal static class RebuildRecordsCommand
{
    public const string Verb = "rebuild-records";

    private const string Usage = "usage: dotnet run -- rebuild-records --user <athlete-guid>";

    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (args is not [Verb, "--user", var user] || !Guid.TryParse(user, out var userId))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        using var host = Host.CreateApplicationBuilder().AddLionheartDatabase().Build();
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ModelContext>();

        var rebuilt = await new PersonalRecordRebuild(context, new PersonalRecordDetector(context, TimeProvider.System))
            .RebuildAsync(userId, ct);

        return rebuilt.Match(Describe, Refuse);
    }

    private static int Describe(RecordRebuildReport report)
    {
        Console.WriteLine(Summary(report));
        Console.WriteLine(report.Changed == 0
            ? "The records were already what the history sets, so nothing was written."
            : "Written.");
        return 0;
    }

    /// <summary>The comparison as lines, shared by every command that rebuilds.</summary>
    public static string Summary(RecordRebuildReport report) =>
        $"{report.Before} records before, {report.After} after: {report.Unchanged} unchanged, "
        + $"{report.Restated} restated (beaten on another date, or behind another predecessor), "
        + $"{report.Dropped} no longer set by the history, {report.Established} new.";

    private static int Refuse(Problem problem)
    {
        Console.Error.WriteLine($"rebuild refused: {problem.Description}");
        return 1;
    }
}
