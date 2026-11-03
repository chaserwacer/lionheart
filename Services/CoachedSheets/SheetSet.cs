using System.Globalization;
using System.Text.RegularExpressions;
using lionheart.Functional;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.CoachedSheets;

/// <summary>
/// Which programme sheet a set came from and the week within it, as training-os tags it:
/// <c>sheet:mk-2#w1</c>.
/// </summary>
public sealed partial record SheetSource(string Sheet, int Week)
{
    /// <summary>The source a tag names, or why it names none.</summary>
    public static Result<SheetSource, string> Parse(string tag) =>
        Tag().Match(tag) is { Success: true } match
        && int.TryParse(match.Groups["week"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var week)
            ? Result<SheetSource, string>.Success(new SheetSource(match.Groups["sheet"].Value, week))
            : Result<SheetSource, string>.Failure($"'{tag}' is not a programme sheet's source, sheet:<sheet>#w<week>");

    public override string ToString() => $"sheet:{Sheet}#w{Week}";

    [GeneratedRegex("^sheet:(?<sheet>[a-z0-9-]+)#w(?<week>[0-9]+)$")]
    private static partial Regex Tag();
}

/// <summary>
/// One set of a coached programme as training-os derived it from the athlete's spreadsheet.
/// </summary>
/// <remarks>
/// <see cref="Session"/> is training-os's own session id — the date, the sheet and the day — which
/// is what groups sets into a workout. <see cref="Exercise"/> is its slug for the lift, the name
/// the movement map is keyed by; <see cref="ExerciseAsWritten"/> is the cell as typed in the sheet.
/// Weights are pounds, the unit the spreadsheets and training-os keep. <see cref="Note"/> is the
/// coach's prescription and notes for the row, empty when there are none.
/// </remarks>
public sealed record SheetSet(
    int Line,
    string Session,
    DateOnly Date,
    string Day,
    SheetSource Source,
    string Exercise,
    string ExerciseAsWritten,
    double Weight,
    int Reps,
    Rpe Rpe,
    string Note)
{
    public const WeightUnit Unit = WeightUnit.Pounds;
}

/// <summary>The programme-sheet sets in a training-os set log, and how many Strong rows it set aside.</summary>
/// <remarks>
/// The log holds the athlete's Strong history beside the sheets. That history imports through the
/// Strong importer from Strong's own export, so here its rows are only counted.
/// </remarks>
public sealed record SheetLog(IReadOnlyList<SheetSet> Sets, int StrongRowsSetAside);
