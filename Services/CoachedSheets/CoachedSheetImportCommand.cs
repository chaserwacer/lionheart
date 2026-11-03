using System.Diagnostics;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Services.Import;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace lionheart.Services.CoachedSheets;

/// <summary>
/// The command line the coached-programme import runs behind:
/// <c>dotnet run -- import-coached-sheets &lt;training-os/derived/sets.tsv&gt; --user &lt;athlete-id&gt;</c>.
/// </summary>
/// <remarks>
/// Packaged like the Strong import, for its reasons: a filesystem path, an athlete named on the
/// command line, and no bulk-write route to keep out of production. Sessions are placed at noon in
/// this machine's time zone, as it applied on each date.
/// </remarks>
internal static class CoachedSheetImportCommand
{
    public const string Verb = "import-coached-sheets";

    private const string Usage =
        "usage: dotnet run -- import-coached-sheets <training-os/derived/sets.tsv> --user <athlete-guid>";

    public static async Task<int> RunAsync(string[] args, CancellationToken ct)
    {
        if (args is not [Verb, var log, "--user", var user] || !Guid.TryParse(user, out var userId))
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        using var host = Host.CreateApplicationBuilder().AddLionheartDatabase().Build();
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ModelContext>();
        var clock = TimeProvider.System;
        var rebuild = new PersonalRecordRebuild(context, new PersonalRecordDetector(context, clock));

        var imported = await SheetMovementMap.Shipped()
            .Bind(movements => SheetDecisions.Shipped().Map(decisions => (Movements: movements, Decisions: decisions)))
            .BindAsync(files => new CoachedSheetImporter(context, rebuild, files.Movements, files.Decisions, TimeZoneInfo.Local, clock)
                .ImportAsync(log, userId, ct));

        return imported.Match(Describe, Refuse);
    }

    // ---- Reporting ----

    private static int Describe(CoachedSheetImportReport report)
    {
        Console.WriteLine($"Sessions placed at noon in {TimeZoneInfo.Local.Id}.");
        Console.WriteLine($"{report.SheetSetsInLog} programme-sheet sets in the log; {report.StrongRowsSetAside} Strong rows set aside (Strong history imports through import-strong).");
        foreach (var tally in report.NotPerformed)
        {
            Console.WriteLine($"  not counted, {Reason(tally.Kind)}: {tally.Sets} sets from {tally.Rows} rows");
        }

        Console.WriteLine($"  {report.ClustersSplit} cluster sets stored as their singles.");
        Console.WriteLine($"{report.SessionsInLog} sessions with performed sets ({report.SessionsWithNothingPerformed} more had none), {report.AlreadyImported} already imported.");
        Console.WriteLine($"{report.SessionsCreated} sessions created, {report.SetsCreated} sets.");

        Console.WriteLine($"\n{report.ProgramsCreated.Count} programmes created.");
        foreach (var program in report.ProgramsCreated) Console.WriteLine($"  {program}");

        ImportConsole.Growth(report.Catalogue);
        ImportConsole.Unsure(report.UnsureMappings, SheetMovementMap.File);

        Console.WriteLine(report.Records switch
        {
            RecordsOutcome.Rebuilt rebuilt => $"\nRecords rebuilt over the whole history. {RebuildRecordsCommand.Summary(rebuilt.Report)}",
            RecordsOutcome.Untouched => "\nNo session added, so the records were left as they were.",
            _ => throw new UnreachableException(),
        });

        return 0;
    }

    private static string Reason(NotPerformedKind kind) => kind switch
    {
        NotPerformedKind.Computed => "weight computed by the spreadsheet",
        NotPerformedKind.Placeholder => "placeholder",
        NotPerformedKind.Contradicted => "contradicted by the athlete's note",
        NotPerformedKind.Implausible => "implausible weight",
        _ => throw new UnreachableException(),
    };

    private static int Refuse(Problem problem)
    {
        Console.Error.WriteLine($"import refused: {problem.Description}");
        return 1;
    }
}
