using System.Globalization;
using System.Text;
using lionheart.Functional;
using lionheart.Model.Training.Sets;

namespace lionheart.Services.CoachedSheets;

/// <summary>One line of a training-os set log that could not be read, and why.</summary>
public sealed record SheetLogDefect(int Line, string Reason);

/// <summary>
/// Reads training-os's set log (<c>derived/sets.tsv</c>) for the sets its programme sheets hold.
/// </summary>
/// <remarks>
/// Tab-separated with a header row: training-os writes tabs out of every note, so a tab only ever
/// separates fields. Columns are found by their header, so a reordered file reads the same.
/// <para>
/// All or nothing, for the reason the Strong reader is: a row is a set inside a session, and
/// carrying on past one that cannot be read would store a session that differs from what was done.
/// A Strong row is counted and set aside; a row from any other source is refused, so a source the
/// log grows later is said out loud rather than quietly skipped.
/// </para>
/// </remarks>
internal static class SheetLogReader
{
    private const string StrongSourcePrefix = "strong:";

    private static readonly string[] Columns =
        ["date", "session_id", "workout", "exercise", "exercise_raw", "weight", "reps", "rpe", "set_note", "source"];

    public static Result<SheetLog, Problem> Read(string path) =>
        Existing(path)
            .Bind(Lines)
            .Bind(lines => Accepted(lines.Partition(Row)))
            .Map(rows => new SheetLog(
                [.. rows.OfType<LogRow.Sheet>().Select(row => row.Set)],
                rows.Count(row => row is LogRow.Strong)));

    private static Result<string, Problem> Existing(string path) =>
        File.Exists(path)
            ? Result<string, Problem>.Success(path)
            : Result<string, Problem>.Failure(new Problem.Invalid("sets", $"No file at '{path}'."));

    /// <summary>Every row after the header, as its fields by column name, or why it cannot be split.</summary>
    private static Result<IReadOnlyList<Result<LogLine, SheetLogDefect>>, Problem> Lines(string path)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length == 0) return Result<IReadOnlyList<Result<LogLine, SheetLogDefect>>, Problem>.Failure(
            new Problem.Invalid("sets", $"'{path}' is empty."));

        var header = lines[0].Split('\t');
        var missing = Columns.Except(header).ToList();
        if (missing.Count > 0) return Result<IReadOnlyList<Result<LogLine, SheetLogDefect>>, Problem>.Failure(
            new Problem.Invalid("sets",
                $"This is not a training-os set log: it has no {string.Join(", ", missing.Select(column => $"'{column}'"))} column."));

        var position = Columns.ToDictionary(column => column, column => Array.IndexOf(header, column));

        return Result<IReadOnlyList<Result<LogLine, SheetLogDefect>>, Problem>.Success(
        [
            .. lines
                .Select((text, index) => (Text: text, Number: index + 1))
                .Skip(1)
                .Where(line => line.Text.Length > 0)
                .Select(line => line.Text.Split('\t') is var fields && fields.Length == header.Length
                    ? Result<LogLine, SheetLogDefect>.Success(new LogLine(
                        line.Number, position.ToDictionary(column => column.Key, column => fields[column.Value])))
                    : Defect<LogLine>(line.Number, $"has {fields.Length} fields where the header has {header.Length}"))
        ]);
    }

    // ---- One row ----

    private static Result<LogRow, SheetLogDefect> Row(Result<LogLine, SheetLogDefect> read) =>
        read.Bind(line => line["source"] switch
        {
            var source when source.StartsWith(StrongSourcePrefix, StringComparison.Ordinal) =>
                Result<LogRow, SheetLogDefect>.Success(new LogRow.Strong()),
            var source => SheetSource.Parse(source)
                .MapError(reason => new SheetLogDefect(line.Number, $"has a source {reason}"))
                .Bind(sheet => Set(line, sheet))
                .Map(LogRow (set) => new LogRow.Sheet(set)),
        });

    private static Result<SheetSet, SheetLogDefect> Set(LogLine line, SheetSource source) =>
        Date(line).Bind(date =>
            Weight(line).Bind(weight =>
                Reps(line).Bind(reps =>
                    Rating(line).Bind(rpe =>
                        Named(line, "exercise").Bind(exercise =>
                            Named(line, "session_id").Map(session => new SheetSet(
                                line.Number,
                                session,
                                date,
                                line["workout"].Trim(),
                                source,
                                exercise,
                                line["exercise_raw"].Trim(),
                                weight,
                                reps,
                                rpe,
                                line["set_note"].Trim())))))));

    private static Result<DateOnly, SheetLogDefect> Date(LogLine line) =>
        DateOnly.TryParseExact(line["date"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? Result<DateOnly, SheetLogDefect>.Success(date)
            : Defect<DateOnly>(line.Number, $"has a date '{line["date"]}' that is not yyyy-MM-dd");

    /// <remarks>Above zero: training-os only keeps a sheet row whose weight was filled in.</remarks>
    private static Result<double, SheetLogDefect> Weight(LogLine line) =>
        double.TryParse(line["weight"], NumberStyles.Float, CultureInfo.InvariantCulture, out var weight)
        && double.IsFinite(weight) && weight > 0
            ? Result<double, SheetLogDefect>.Success(weight)
            : Defect<double>(line.Number, $"has a weight '{line["weight"]}' that is not a load above zero");

    private static Result<int, SheetLogDefect> Reps(LogLine line) =>
        int.TryParse(line["reps"], NumberStyles.None, CultureInfo.InvariantCulture, out var reps) && reps > 0
            ? Result<int, SheetLogDefect>.Success(reps)
            : Defect<int>(line.Number, $"has reps '{line["reps"]}' that are not a whole count above zero");

    /// <remarks>
    /// The rating the athlete wrote in an "RPE Actual" column; training-os keeps the coach's
    /// target only in the note, so a number here is a performance.
    /// </remarks>
    private static Result<Rpe, SheetLogDefect> Rating(LogLine line)
    {
        var text = line["rpe"];
        if (text.Length == 0) return Result<Rpe, SheetLogDefect>.Success(Rpe.Unrated);

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
               && value is >= 0 and <= 10
            ? Result<Rpe, SheetLogDefect>.Success(Rpe.Of(value))
            : Defect<Rpe>(line.Number, $"has an RPE '{text}' outside 0 to 10");
    }

    private static Result<string, SheetLogDefect> Named(LogLine line, string column) =>
        line[column].Trim() is { Length: > 0 } value
            ? Result<string, SheetLogDefect>.Success(value)
            : Defect<string>(line.Number, $"has no {column}");

    // ---- Refusal ----

    private static Result<IReadOnlyList<T>, Problem> Accepted<T>(Partitioned<T, SheetLogDefect> read) =>
        read.Refused.Count == 0
            ? Result<IReadOnlyList<T>, Problem>.Success(read.Delivered)
            : Result<IReadOnlyList<T>, Problem>.Failure(new Problem.Invalid(
                [.. read.Refused.Select(defect => new InvalidField($"line {defect.Line}", $"Line {defect.Line} {defect.Reason}."))]));

    private static Result<T, SheetLogDefect> Defect<T>(int line, string reason) =>
        Result<T, SheetLogDefect>.Failure(new SheetLogDefect(line, reason));

    // ---- Shapes local to reading ----

    private sealed record LogLine(int Number, IReadOnlyDictionary<string, string> Fields)
    {
        public string this[string column] => Fields[column];
    }

    /// <summary>A row is a programme-sheet set, or a Strong row this import leaves to the Strong importer.</summary>
    private abstract record LogRow
    {
        private protected LogRow() { }

        public sealed record Sheet(SheetSet Set) : LogRow;

        public sealed record Strong : LogRow;
    }
}
