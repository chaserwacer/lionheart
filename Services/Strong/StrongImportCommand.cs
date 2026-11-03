using lionheart.Data;
using lionheart.Functional;
using lionheart.Services.Import;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace lionheart.Services.Strong;

/// <summary>
/// The command line the Strong import runs behind:
/// <c>dotnet run -- import-strong &lt;export.csv&gt; --user &lt;athlete-id&gt;</c>.
/// </summary>
/// <remarks>
/// A command rather than an endpoint for the reasons the Oura archive import is one: it takes a
/// filesystem path, names its athlete on the command line, and adds no bulk-write route that would
/// have to be kept out of production. It builds a host with configuration and the database only.
/// <para>
/// Times in the export are read in this machine's time zone, as it applied on each date — the
/// zone the athlete's phone was keeping when Strong wrote them down.
/// </para>
/// </remarks>
internal static class StrongImportCommand
{
    public const string Verb = "import-strong";

    private const string Usage =
        "usage: dotnet run -- import-strong <strong-export.csv> --user <athlete-guid>";

    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (Arguments.Parse(args) is not { } arguments)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        using var host = Host.CreateApplicationBuilder().AddLionheartDatabase().Build();
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ModelContext>();
        var clock = TimeProvider.System;

        var imported = await StrongMovementMap.Shipped()
            .BindAsync(map => new StrongImporter(
                    context, new PersonalRecordDetector(context, clock), map, TimeZoneInfo.Local, clock)
                .ImportAsync(arguments.Export, arguments.UserId, ct));

        return imported.Match(Describe, Refuse);
    }

    private sealed record Arguments(string Export, Guid UserId)
    {
        /// <summary>The arguments, or nothing if they do not form a runnable command.</summary>
        public static Arguments? Parse(string[] args)
        {
            if (args is not [Verb, var export, "--user", var user]) return null;

            return Guid.TryParse(user, out var userId) ? new Arguments(export, userId) : null;
        }
    }

    // ---- Reporting ----

    private static int Describe(StrongImportReport report)
    {
        Console.WriteLine($"Times read in {TimeZoneInfo.Local.Id}.");
        Console.WriteLine($"{report.WorkoutsInExport} workouts in the export, {report.AlreadyImported} already imported.");
        Console.WriteLine($"{report.SessionsCreated} sessions created.");
        Console.WriteLine(
            $"{report.Sets.Lifts} lifts ({report.Sets.Drops} drop sets, {report.Sets.Failures} to failure), "
            + $"{report.Sets.WarmUps} warm-ups kept as freeform sets, {report.Sets.RestTimers} rest timers set aside.");
        Console.WriteLine($"{report.RecordsEstablished} personal records established.");

        ImportConsole.Growth(report.Catalogue);
        ImportConsole.Unsure(report.UnsureMappings, StrongMovementMap.File);

        return 0;
    }

    private static int Refuse(Problem problem)
    {
        Console.Error.WriteLine($"import refused: {problem.Description}");
        return 1;
    }
}
