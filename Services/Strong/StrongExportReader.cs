using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Sets;
using lionheart.Services.Import;

namespace lionheart.Services.Strong;

/// <summary>
/// Reads a Strong CSV export into workouts, each with its start placed in a time zone.
/// </summary>
/// <remarks>
/// All or nothing: one unreadable line refuses the whole export and every such line is named.
/// The Oura archive importer carries on past a bad record because each record there stands alone.
/// A row here is a set inside a workout, so carrying on would store a session that silently
/// differs from what was done, and an imported workout is never read again to put it right.
/// Records are also replayed oldest first, which a workout arriving on a later run would break.
/// <para>
/// Only lifts are read. A row carrying a distance or a time is refused rather than stored without
/// it, and so is a negative weight, which Strong uses for assistance and Lionheart would read as
/// load. The export holds neither today; this is so that one appearing later is said out loud.
/// </para>
/// </remarks>
internal static partial class StrongExportReader
{
    private static readonly string[] Columns =
    [
        "Date", "Workout Name", "Duration", "Exercise Name", "Set Order", "Weight", "Reps",
        "Distance", "Seconds", "Notes", "Workout Notes", "RPE"
    ];

    public static Result<IReadOnlyList<StrongWorkout>, Problem> Read(string path, TimeZoneInfo zone) =>
        Existing(path)
            .Bind(Lines)
            .Bind(lines => LineDefects.Accepted(lines.Partition(line => line.Bind(read => Row(read, zone)))))
            .Bind(rows => LineDefects.Accepted(rows.GroupBy(row => row.Workout).Partition(Workout)));

    // ---- The file ----

    private static Result<string, Problem> Existing(string path) =>
        File.Exists(path)
            ? Result<string, Problem>.Success(path)
            : Result<string, Problem>.Failure(new Problem.Invalid("export", $"No file at '{path}'."));

    /// <summary>
    /// Every record in the file, keyed by column name, or the reason a line could not be split.
    /// </summary>
    /// <remarks>
    /// Columns are found by their header rather than their position, so a reordered export reads
    /// the same. Fields are not trimmed: an exercise name's trailing space is Strong's, and is
    /// removed where names are compared rather than lost here.
    /// </remarks>
    private static Result<IReadOnlyList<Result<CsvLine, LineDefect>>, Problem> Lines(string path)
    {
        var records = CsvRecords.Read(File.ReadAllText(path, Encoding.UTF8)).ToList();

        return Header(records).Map(IReadOnlyList<Result<CsvLine, LineDefect>> (header) =>
            [.. records.Skip(1).Select(record => record.Bind(header.Line))]);
    }

    /// <summary>Where each column Strong writes sits in this file, and how many fields a row has.</summary>
    private static Result<CsvHeader, Problem> Header(IReadOnlyList<Result<CsvRecord, LineDefect>> records) =>
        (records.FirstOrDefault()
         ?? Result<CsvRecord, LineDefect>.Failure(new LineDefect(1, "is empty")))
        .MapError(Problem (defect) => new Problem.Invalid(
            "export", $"The export's header cannot be read: line {defect.Line} {defect.Reason}."))
        .Ensure(
            record => !Columns.Except(record.Fields).Any(),
            record => new Problem.Invalid("export",
                $"This is not a Strong export: it has no {string.Join(", ", Columns.Except(record.Fields).Select(c => $"'{c}'"))} column."))
        .Map(record => new CsvHeader(
            record.Fields.Count,
            Columns.ToDictionary(column => column, column => record.Fields.ToList().IndexOf(column))));

    // ---- One row ----

    private static Result<StrongRow, LineDefect> Row(CsvLine line, TimeZoneInfo zone) =>
        Start(line, zone).Bind(start =>
            Duration(line).Bind(duration =>
                Content(line).Map(content => new StrongRow(
                    line.Number,
                    new StrongWorkoutKey(start.DateTime, line["Workout Name"]),
                    new Interval(start, start + duration),
                    Unescaped(line["Workout Notes"]),
                    content))));

    /// <summary>
    /// When the workout began, at the offset the athlete's clock was keeping that day.
    /// </summary>
    /// <remarks>
    /// Strong exports wall-clock time with no offset. Placing it in the zone as it applied on that
    /// date, rather than at today's offset, is what keeps a summer workout and a winter one each
    /// on the day they happened. A time inside a daylight-saving change is refused rather than
    /// resolved: in the hour that was skipped it never happened, and in the hour that was repeated
    /// it happened twice, so either answer would be a guess.
    /// </remarks>
    private static Result<DateTimeOffset, LineDefect> Start(CsvLine line, TimeZoneInfo zone)
    {
        var text = line["Date"];
        if (!DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local))
        {
            return LineDefects.Defect<DateTimeOffset>(line.Number, $"has a date '{text}' that is not yyyy-MM-dd HH:mm:ss");
        }

        if (zone.IsInvalidTime(local))
        {
            return LineDefects.Defect<DateTimeOffset>(line.Number,
                $"starts at {text}, which never happened in {zone.Id}: the clocks went forward past it");
        }

        return zone.IsAmbiguousTime(local)
            ? LineDefects.Defect<DateTimeOffset>(line.Number,
                $"starts at {text}, which happened twice in {zone.Id}: the clocks went back over it")
            : Result<DateTimeOffset, LineDefect>.Success(new DateTimeOffset(local, zone.GetUtcOffset(local)));
    }

    /// <summary>Strong's "1h 5m", "47m", "1h" or "27s".</summary>
    private static Result<TimeSpan, LineDefect> Duration(CsvLine line)
    {
        var text = line["Duration"];
        var match = DurationPattern().Match(text);

        return match.Success && text.Length > 0
            ? Result<TimeSpan, LineDefect>.Success(new TimeSpan(
                Part(match, "h"), Part(match, "m"), Part(match, "s")))
            : LineDefects.Defect<TimeSpan>(line.Number, $"has a duration '{text}' that is not of the form 1h 5m");
    }

    private static int Part(Match match, string unit) =>
        match.Groups[unit].Success ? int.Parse(match.Groups[unit].Value, CultureInfo.InvariantCulture) : 0;

    [GeneratedRegex(@"^(?:(?<h>\d+)h)?\s*(?:(?<m>\d+)m)?\s*(?:(?<s>\d+)s)?$")]
    private static partial Regex DurationPattern();

    private static Result<RowContent, LineDefect> Content(CsvLine line) =>
        line["Set Order"] switch
        {
            "Rest Timer" => Result<RowContent, LineDefect>.Success(new RowContent.RestTimer()),
            "W" => Set(line, StrongSetKind.WarmUp),
            "D" => Set(line, StrongSetKind.Drop),
            "F" => Set(line, StrongSetKind.Failure),
            var order when int.TryParse(order, NumberStyles.None, CultureInfo.InvariantCulture, out _) =>
                Set(line, StrongSetKind.Working),
            var order => LineDefects.Defect<RowContent>(line.Number, $"has a set order '{order}' Strong does not write"),
        };

    private static Result<RowContent, LineDefect> Set(CsvLine line, StrongSetKind kind) =>
        Exercise(line).Bind(exercise =>
            Weight(line).Bind(weight =>
                Reps(line).Bind(reps =>
                    Rating(line).Bind(rpe =>
                        LiftOnly(line).Map(RowContent (_) => new RowContent.Set(new StrongSet(
                            exercise, kind, weight, reps, rpe, Unescaped(line["Notes"]))))))));

    private static Result<string, LineDefect> Exercise(CsvLine line) =>
        line["Exercise Name"].Trim() is { Length: > 0 } name
            ? Result<string, LineDefect>.Success(name)
            : LineDefects.Defect<string>(line.Number, "names no exercise");

    private static Result<double, LineDefect> Weight(CsvLine line) =>
        Number(line, "Weight").Bind(weight => weight >= 0
            ? Result<double, LineDefect>.Success(weight)
            : LineDefects.Defect<double>(line.Number,
                $"has a weight of {weight}: a negative weight is assistance, which Lionheart would read as load"));

    private static Result<int, LineDefect> Reps(CsvLine line) =>
        Number(line, "Reps").Bind(reps => reps >= 0 && reps == Math.Floor(reps) && reps <= int.MaxValue
            ? Result<int, LineDefect>.Success((int)reps)
            : LineDefects.Defect<int>(line.Number, $"has {reps} reps, which is not a whole count"));

    private static Result<Rpe, LineDefect> Rating(CsvLine line)
    {
        var text = line["RPE"];
        if (text.Length == 0) return Result<Rpe, LineDefect>.Success(Rpe.Unrated);

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
               && value is >= 0 and <= 10
            ? Result<Rpe, LineDefect>.Success(Rpe.Of(value))
            : LineDefects.Defect<Rpe>(line.Number, $"has an RPE '{text}' outside 0 to 10");
    }

    /// <summary>Refuses a timed or distance set rather than storing it as a lift without its measure.</summary>
    private static Result<Unit, LineDefect> LiftOnly(CsvLine line) =>
        Number(line, "Distance").Bind(distance =>
            Number(line, "Seconds").Bind(seconds => distance == 0 && seconds == 0
                ? Result<Unit, LineDefect>.Success(Unit.Value)
                : LineDefects.Defect<Unit>(line.Number,
                    "records a distance or a time; only lifts are imported, and storing it as one would lose the measure")));

    private static Result<double, LineDefect> Number(CsvLine line, string column)
    {
        var text = line[column];
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
               && double.IsFinite(value)
            ? Result<double, LineDefect>.Success(value)
            : LineDefects.Defect<double>(line.Number, $"has a {column.ToLowerInvariant()} '{text}' that is not a number");
    }

    /// <summary>Strong writes a line break inside a note as a backslash and an n.</summary>
    private static string Unescaped(string note) => note.Replace("\\n", "\n").Trim();

    // ---- One workout ----

    /// <summary>
    /// The rows sharing one key, as one workout — provided they agree on what is the workout's.
    /// </summary>
    /// <remarks>
    /// Strong repeats the duration on every row and writes the workout's notes on its first, so
    /// two rows of one workout disagreeing means the key is not identifying what it should.
    /// </remarks>
    private static Result<StrongWorkout, LineDefect> Workout(IGrouping<StrongWorkoutKey, StrongRow> rows)
    {
        var first = rows.First();

        if (rows.FirstOrDefault(row => row.Period != first.Period) is { } disagreeing)
        {
            return LineDefects.Defect<StrongWorkout>(disagreeing.Line,
                $"gives workout '{first.Workout.Name}' of {first.Workout.Start:yyyy-MM-dd HH:mm:ss} a different duration from line {first.Line}");
        }

        var notes = rows.Select(row => row.WorkoutNotes).Where(note => note.Length > 0).Distinct().ToList();
        if (notes.Count > 1)
        {
            return LineDefects.Defect<StrongWorkout>(first.Line,
                $"workout '{first.Workout.Name}' of {first.Workout.Start:yyyy-MM-dd HH:mm:ss} carries two different workout notes");
        }

        return Result<StrongWorkout, LineDefect>.Success(new StrongWorkout(
            rows.Key,
            first.Period,
            notes.SingleOrDefault() ?? string.Empty,
            [.. rows.Select(row => row.Content).OfType<RowContent.Set>().Select(set => set.Value)],
            rows.Count(row => row.Content is RowContent.RestTimer)));
    }

    // ---- Shapes local to reading ----

    private sealed record StrongRow(
        int Line, StrongWorkoutKey Workout, Interval Period, string WorkoutNotes, RowContent Content);

    /// <summary>A row is a set, or the rest Strong timed after one.</summary>
    private abstract record RowContent
    {
        private protected RowContent() { }

        public sealed record Set(StrongSet Value) : RowContent;

        public sealed record RestTimer : RowContent;
    }
}
