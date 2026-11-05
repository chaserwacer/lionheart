using System.Text;
using lionheart.Functional;

namespace lionheart.Services.Import;

/// <summary>One record of a CSV file and the line it starts on.</summary>
internal sealed record CsvRecord(int Line, IReadOnlyList<string> Fields);

/// <summary>
/// Splits CSV text into records, the way RFC 4180, Strong's export and Strava's archive write it:
/// comma-separated, a field in double quotes may hold commas and line breaks, and a doubled quote
/// inside one is a quote.
/// </summary>
/// <remarks>
/// Written here rather than taken from the framework because the line a record starts on is how a
/// defect is reported, and the framework's parser loses it: after a blank line it reports the
/// blank line's number for the record that follows. Blank lines are skipped here too, without
/// disturbing the numbering of the records around them. A quote still open at the end of the text
/// leaves its record unsplittable, and is reported against the line the record began on.
/// </remarks>
internal static class CsvRecords
{
    public static IEnumerable<Result<CsvRecord, LineDefect>> Read(string text)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var line = 1;
        var start = 1;
        var quoted = false;
        var begun = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (quoted)
            {
                var escapedQuote = c == '"' && i + 1 < text.Length && text[i + 1] == '"';
                if (escapedQuote)
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    if (c == '\n') line++;
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    quoted = begun = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    begun = true;
                    break;
                case '\r':
                    break;
                case '\n':
                    if (begun || field.Length > 0) yield return Ended();
                    line++;
                    start = line;
                    break;
                default:
                    field.Append(c);
                    begun = true;
                    break;
            }
        }

        if (quoted)
        {
            yield return Result<CsvRecord, LineDefect>.Failure(
                new LineDefect(start, "opens a quote that is never closed"));
        }
        else if (begun || field.Length > 0)
        {
            yield return Ended();
        }

        Result<CsvRecord, LineDefect> Ended()
        {
            fields.Add(field.ToString());
            var record = new CsvRecord(start, [.. fields]);
            fields.Clear();
            field.Clear();
            begun = false;
            return Result<CsvRecord, LineDefect>.Success(record);
        }
    }
}
