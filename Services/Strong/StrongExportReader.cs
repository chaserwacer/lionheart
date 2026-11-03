using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.Strong;

/// <summary>One line of a Strong export that could not be read, and why.</summary>
public sealed record StrongExportDefect(int Line, string Reason);

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
            .Bind(lines => Accepted(lines.Partition(line => line.Bind(read => Row(read, zone)))))
            .Bind(rows => Accepted(rows.GroupBy(row => row.Workout).Partition(Workout)));

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
    private static Result<IReadOnlyList<Result<CsvLine, StrongExportDefect>>, Problem> Lines(string path)
    {
        var records = CsvRecords.Read(File.ReadAllText(path, Encoding.UTF8)).ToList();
        var header = records.FirstOrDefault()?.Match(record => record.Fields, _ => []) ?? [];

        var missing = Columns.Except(header).ToList();
        if (missing.Count > 0)
        {
            return Result<IReadOnlyList<Result<CsvLine, StrongExportDefect>>, Problem>.Failure(
                new Problem.Invalid("export",
                    $"This is not a Strong export: it has no {string.Join(", ", missing.Select(c => $"'{c}'"))} column."));
        }

        var position = Columns.ToDictionary(column => column, column => header.ToList().IndexOf(column));

        return Result<IReadOnlyList<Result<CsvLine, StrongExportDefect>>, Problem>.Success(
            [.. records.Skip(1).Select(record => record.Bind(read => Line(read, header.Count, position)))]);
    }

    private static Result<CsvLine, StrongExportDefect> Line(
        CsvRecord record, int width, IReadOnlyDictionary<string, int> position) =>
        record.Fields.Count == width
            ? Result<CsvLine, StrongExportDefect>.Success(new CsvLine(
                record.Line, position.ToDictionary(column => column.Key, column => record.Fields[column.Value])))
            : Defect<CsvLine>(record.Line, $"has {record.Fields.Count} fields where the header has {width}");

    // ---- One row ----

    private static Result<StrongRow, StrongExportDefect> Row(CsvLine line, TimeZoneInfo zone) =>
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
    private static Result<DateTimeOffset, StrongExportDefect> Start(CsvLine line, TimeZoneInfo zone)
    {
        var text = line["Date"];
        if (!DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var local))
        {
            return Defect<DateTimeOffset>(line.Number, $"has a date '{text}' that is not yyyy-MM-dd HH:mm:ss");
        }

        if (zone.IsInvalidTime(local))
        {
            return Defect<DateTimeOffset>(line.Number,
                $"starts at {text}, which never happened in {zone.Id}: the clocks went forward past it");
        }

        return zone.IsAmbiguousTime(local)
            ? Defect<DateTimeOffset>(line.Number,
                $"starts at {text}, which happened twice in {zone.Id}: the clocks went back over it")
            : Result<DateTimeOffset, StrongExportDefect>.Success(new DateTimeOffset(local, zone.GetUtcOffset(local)));
    }

    /// <summary>Strong's "1h 5m", "47m", "1h" or "27s".</summary>
    private static Result<TimeSpan, StrongExportDefect> Duration(CsvLine line)
    {
        var text = line["Duration"];
        var match = DurationPattern().Match(text);

        return match.Success && text.Length > 0
            ? Result<TimeSpan, StrongExportDefect>.Success(new TimeSpan(
                Part(match, "h"), Part(match, "m"), Part(match, "s")))
            : Defect<TimeSpan>(line.Number, $"has a duration '{text}' that is not of the form 1h 5m");
    }

    private static int Part(Match match, string unit) =>
        match.Groups[unit].Success ? int.Parse(match.Groups[unit].Value, CultureInfo.InvariantCulture) : 0;

    [GeneratedRegex(@"^(?:(?<h>\d+)h)?\s*(?:(?<m>\d+)m)?\s*(?:(?<s>\d+)s)?$")]
    private static partial Regex DurationPattern();

    private static Result<RowContent, StrongExportDefect> Content(CsvLine line) =>
        line["Set Order"] switch
        {
            "Rest Timer" => Result<RowContent, StrongExportDefect>.Success(new RowContent.RestTimer()),
            "W" => Set(line, StrongSetKind.WarmUp),
            "D" => Set(line, StrongSetKind.Drop),
            "F" => Set(line, StrongSetKind.Failure),
            var order when int.TryParse(order, NumberStyles.None, CultureInfo.InvariantCulture, out _) =>
                Set(line, StrongSetKind.Working),
            var order => Defect<RowContent>(line.Number, $"has a set order '{order}' Strong does not write"),
        };

    private static Result<RowContent, StrongExportDefect> Set(CsvLine line, StrongSetKind kind) =>
        Exercise(line).Bind(exercise =>
            Weight(line).Bind(weight =>
                Reps(line).Bind(reps =>
                    Rating(line).Bind(rpe =>
                        LiftOnly(line).Map(RowContent (_) => new RowContent.Set(new StrongSet(
                            exercise, kind, weight, reps, rpe, Unescaped(line["Notes"]))))))));

    private static Result<string, StrongExportDefect> Exercise(CsvLine line) =>
        line["Exercise Name"].Trim() is { Length: > 0 } name
            ? Result<string, StrongExportDefect>.Success(name)
            : Defect<string>(line.Number, "names no exercise");

    private static Result<double, StrongExportDefect> Weight(CsvLine line) =>
        Number(line, "Weight").Bind(weight => weight >= 0
            ? Result<double, StrongExportDefect>.Success(weight)
            : Defect<double>(line.Number,
                $"has a weight of {weight}: a negative weight is assistance, which Lionheart would read as load"));

    private static Result<int, StrongExportDefect> Reps(CsvLine line) =>
        Number(line, "Reps").Bind(reps => reps >= 0 && reps == Math.Floor(reps) && reps <= int.MaxValue
            ? Result<int, StrongExportDefect>.Success((int)reps)
            : Defect<int>(line.Number, $"has {reps} reps, which is not a whole count"));

    private static Result<Rpe, StrongExportDefect> Rating(CsvLine line)
    {
        var text = line["RPE"];
        if (text.Length == 0) return Result<Rpe, StrongExportDefect>.Success(Rpe.Unrated);

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
               && value is >= 0 and <= 10
            ? Result<Rpe, StrongExportDefect>.Success(Rpe.Of(value))
            : Defect<Rpe>(line.Number, $"has an RPE '{text}' outside 0 to 10");
    }

    /// <summary>Refuses a timed or distance set rather than storing it as a lift without its measure.</summary>
    private static Result<Unit, StrongExportDefect> LiftOnly(CsvLine line) =>
        Number(line, "Distance").Bind(distance =>
            Number(line, "Seconds").Bind(seconds => distance == 0 && seconds == 0
                ? Result<Unit, StrongExportDefect>.Success(Unit.Value)
                : Defect<Unit>(line.Number,
                    "records a distance or a time; only lifts are imported, and storing it as one would lose the measure")));

    private static Result<double, StrongExportDefect> Number(CsvLine line, string column)
    {
        var text = line[column];
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
               && double.IsFinite(value)
            ? Result<double, StrongExportDefect>.Success(value)
            : Defect<double>(line.Number, $"has a {column.ToLowerInvariant()} '{text}' that is not a number");
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
    private static Result<StrongWorkout, StrongExportDefect> Workout(IGrouping<StrongWorkoutKey, StrongRow> rows)
    {
        var first = rows.First();

        if (rows.FirstOrDefault(row => row.Period != first.Period) is { } disagreeing)
        {
            return Defect<StrongWorkout>(disagreeing.Line,
                $"gives workout '{first.Workout.Name}' of {first.Workout.Start:yyyy-MM-dd HH:mm:ss} a different duration from line {first.Line}");
        }

        var notes = rows.Select(row => row.WorkoutNotes).Where(note => note.Length > 0).Distinct().ToList();
        if (notes.Count > 1)
        {
            return Defect<StrongWorkout>(first.Line,
                $"workout '{first.Workout.Name}' of {first.Workout.Start:yyyy-MM-dd HH:mm:ss} carries two different workout notes");
        }

        return Result<StrongWorkout, StrongExportDefect>.Success(new StrongWorkout(
            rows.Key,
            first.Period,
            notes.SingleOrDefault() ?? string.Empty,
            [.. rows.Select(row => row.Content).OfType<RowContent.Set>().Select(set => set.Value)],
            rows.Count(row => row.Content is RowContent.RestTimer)));
    }

    // ---- Refusal ----

    private static Result<IReadOnlyList<T>, Problem> Accepted<T>(Partitioned<T, StrongExportDefect> read) =>
        read.Refused.Count == 0
            ? Result<IReadOnlyList<T>, Problem>.Success(read.Delivered)
            : Result<IReadOnlyList<T>, Problem>.Failure(new Problem.Invalid(
                [.. read.Refused.Select(defect => new InvalidField($"line {defect.Line}", $"Line {defect.Line} {defect.Reason}."))]));

    private static Result<T, StrongExportDefect> Defect<T>(int line, string reason) =>
        Result<T, StrongExportDefect>.Failure(new StrongExportDefect(line, reason));

    // ---- Shapes local to reading ----

    private sealed record CsvLine(int Number, IReadOnlyDictionary<string, string> Fields)
    {
        public string this[string column] => Fields[column];
    }

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
