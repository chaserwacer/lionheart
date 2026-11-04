using lionheart.Model.Training.Patterns;
using lionheart.Services.Training.PersonalRecords;

namespace lionheart.Services.Training.Movement.Library;

/// <summary>A tidy's report as the lines the command prints: every change, by kind, then what it left.</summary>
public static class TidyReportText
{
    public static IEnumerable<string> Lines(TidyReport report)
    {
        var plan = report.Plan;

        yield return report switch
        {
            { Plan.IsEmpty: true } => "The movement library is already tidy; nothing to do.",
            { Run: TidyRun.DryRun } => "Dry run: everything below was done and rolled back, so nothing has been written.",
            _ => "Written.",
        };
        yield return $"Before: {Counts(report.Before)}.";
        yield return $"After:  {Counts(report.After)}.";

        foreach (var part in new[] { MovementPart.Equipment, MovementPart.Base, MovementPart.Modifier })
        foreach (var line in Section(PartHeading(part), plan.Parts
                     .Where(outcome => outcome.Part == part)
                     .OrderBy(outcome => Rank(outcome.Fate))
                     .ThenBy(outcome => outcome.Name, StringComparer.OrdinalIgnoreCase)
                     .Select(PartLine)))
            yield return line;

        var outcomes = plan.Movements.OrderBy(movement => movement.From.ToString(), StringComparer.OrdinalIgnoreCase).ToList();
        var renamed = outcomes.SelectMany(outcome => outcome.Fate is MovementFate.Restated restated
            ? [$"{outcome.From}  →  {MovementNames.Of(restated.To)}{Unsure(restated.Unsure)}"]
            : Array.Empty<string>());
        var merged = outcomes.SelectMany(outcome => outcome.Fate is MovementFate.Merged merged
            ? [$"{outcome.From}  →  into {MovementNames.Of(merged.To)}, moving {merged.Moved}{Unsure(merged.Unsure)}"]
            : Array.Empty<string>());
        var retired = outcomes.SelectMany(outcome => outcome.Fate is MovementFate.Retired retirement
            ? [$"{outcome.From}{Unsure(retirement.Unsure)}"]
            : Array.Empty<string>());

        foreach (var line in Section("Movements renamed", renamed)) yield return line;
        foreach (var line in Section("Movements merged", merged)) yield return line;
        foreach (var line in Section("Movements retired", retired)) yield return line;
        foreach (var line in Section("Movements added", plan.Added.Select(name => MovementNames.Of(name)).Order())) yield return line;
        foreach (var line in Section("What a going base said, carried over", plan.BaseCarries.Select(carry =>
                     $"'{carry.From}' to '{carry.Into}'{(carry.Statement is StatedClassification.Stated ? ": its classification" : string.Empty)}"
                     + (carry.Description.Length > 0 ? $"; its description \"{carry.Description}\"" : string.Empty))))
            yield return line;
        foreach (var line in Section("Setups carried over", plan.SetupCarries.Select(carry => $"onto movement {carry.IntoId}")))
            yield return line;

        yield return string.Empty;
        yield return report.Records switch
        {
            RecordReplay.Replayed replayed => $"Records replayed: {RebuildRecordsCommand.Summary(replayed.Report)}",
            RecordReplay.NotNeeded => "Records not replayed: no movement merged, so none changed movement.",
            _ => throw new System.Diagnostics.UnreachableException(),
        };
        yield return report.Unclassified.Count == 0
            ? "Every base is classified."
            : $"Bases nothing classifies ({report.Unclassified.Count}), whose sets count as unplaced: {string.Join(", ", report.Unclassified)}.";
    }

    private static IEnumerable<string> Section(string heading, IEnumerable<string> lines)
    {
        var listed = lines.ToList();
        if (listed.Count == 0) yield break;

        yield return string.Empty;
        yield return $"{heading} ({listed.Count})";
        foreach (var line in listed) yield return $"  {line}";
    }

    private static string PartLine(PartOutcome outcome) => outcome.Fate switch
    {
        PartFate.Renamed renamed => $"rename  {outcome.Name}  →  {renamed.To}",
        PartFate.Folded folded => $"fold    {outcome.Name}  into  {folded.Into}",
        PartFate.Removed => $"remove  {outcome.Name}  (no movement uses it now)",
        PartFate.Created => $"add     {outcome.Name}",
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static int Rank(PartFate fate) => fate switch
    {
        PartFate.Renamed => 0,
        PartFate.Folded => 1,
        PartFate.Removed => 2,
        PartFate.Created => 3,
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static string PartHeading(MovementPart part) => part switch
    {
        MovementPart.Equipment => "Equipment",
        MovementPart.Base => "Bases",
        MovementPart.Modifier => "Modifiers",
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    private static string Unsure(string unsure) => unsure.Length > 0 ? $"   [unsure: {unsure}]" : string.Empty;

    private static string Counts(LibraryCounts counts) =>
        $"{counts.Movements} movements, {counts.Bases} bases, {counts.Equipment} equipment, {counts.Modifiers} modifiers";
}
