using lionheart.Functional;

namespace lionheart.Services.Import;

/// <summary>One line of an imported file that could not be read, and why.</summary>
public sealed record LineDefect(int Line, string Reason);

/// <summary>Where each column an importer reads sits in a CSV file, and how many fields a row has.</summary>
internal sealed record CsvHeader(int Width, IReadOnlyDictionary<string, int> Position)
{
    /// <summary>A record as a line keyed by the columns read, or a defect when its width is not the header's.</summary>
    public Result<CsvLine, LineDefect> Line(CsvRecord record) =>
        record.Fields.Count == Width
            ? Result<CsvLine, LineDefect>.Success(new CsvLine(
                record.Line, Position.ToDictionary(column => column.Key, column => record.Fields[column.Value])))
            : LineDefects.Defect<CsvLine>(record.Line, $"has {record.Fields.Count} fields where the header has {Width}");
}

/// <summary>One row of a CSV file by column name, and the line it starts on.</summary>
internal sealed record CsvLine(int Number, IReadOnlyDictionary<string, string> Fields)
{
    public string this[string column] => Fields[column];
}

internal static class LineDefects
{
    /// <summary>
    /// Every line read, or a refusal naming each line that could not be — an import file is taken
    /// whole or not at all.
    /// </summary>
    public static Result<IReadOnlyList<T>, Problem> Accepted<T>(Partitioned<T, LineDefect> read) =>
        read.Refused.Count == 0
            ? Result<IReadOnlyList<T>, Problem>.Success(read.Delivered)
            : Result<IReadOnlyList<T>, Problem>.Failure(new Problem.Invalid(
                [.. read.Refused.Select(defect => new InvalidField($"line {defect.Line}", $"Line {defect.Line} {defect.Reason}."))]));

    public static Result<T, LineDefect> Defect<T>(int line, string reason) =>
        Result<T, LineDefect>.Failure(new LineDefect(line, reason));
}
