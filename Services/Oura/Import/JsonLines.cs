namespace lionheart.Services.Oura.Import;

/// <summary>Reads a JSON-lines file one record at a time, keeping each record's line number.</summary>
/// <remarks>
/// Streamed rather than read whole: the activity archive alone is several megabytes on one line
/// per day, and nothing needs two records at once. The line number survives because it is how a
/// rejection is reported — "line 412 of sleep.jsonl" is actionable and "a record in sleep.jsonl"
/// is not. Blank lines are skipped without disturbing the numbering of the records around them.
/// </remarks>
internal static class JsonLines
{
    public static IEnumerable<(int Number, string Text)> Read(string path) =>
        File.ReadLines(path)
            .Select((text, index) => (Number: index + 1, Text: text))
            .Where(line => !string.IsNullOrWhiteSpace(line.Text));
}
