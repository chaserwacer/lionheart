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

    /// <summary>The map from training-os's exercise slugs to movements, <c>coached-movements.json</c>.</summary>
    public static MovementMapFile Movements { get; } = new(
        "coached-movements.json", "lionheart.Services.CoachedSheets.coached-movements.json", "slug");

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

        var imported = await MovementMap.Shipped(Movements)
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

        var growth = report.Catalogue;
        Console.WriteLine($"\n{growth.Movements.Count} movements created.");
        foreach (var movement in growth.Movements.OrderBy(movement => movement.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  {movement}");
        }

        Describe("new movement bases", growth.Bases);
        Describe("new equipment", growth.Equipment);
        Describe("new modifiers", growth.Modifiers);

        if (report.UnsureMappings.Count > 0)
        {
            Console.WriteLine($"\n{report.UnsureMappings.Count} mappings applied that {Movements.FileName} marks unsure:");
            foreach (var mapping in report.UnsureMappings)
            {
                Console.WriteLine($"  {mapping.Name} -> {mapping.Movement}\n      {mapping.Unsure}");
            }
        }

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

    private static void Describe(string what, IReadOnlyList<string> names)
    {
        if (names.Count > 0) Console.WriteLine($"{what}: {string.Join("; ", names.Order(StringComparer.OrdinalIgnoreCase))}");
    }

    private static int Refuse(Problem problem)
    {
        Console.Error.WriteLine($"import refused: {problem.Description}");
        return 1;
    }
}
