using lionheart.Functional;
using lionheart.Services.Import;

namespace lionheart.Tests.Import;

/// <summary>Splitting CSV into records, keeping the line each began on.</summary>
public class CsvRecordsTests
{
    [Fact]
    public void A_quoted_field_keeps_its_commas_and_its_doubled_quotes()
    {
        var record = Single("2025-08-12,\"Squat, paused\",\"he said \"\"slow\"\"\",");

        Assert.Equal(["2025-08-12", "Squat, paused", "he said \"slow\"", ""], record.Fields);
    }

    [Fact]
    public void A_record_is_numbered_by_the_line_it_starts_on_past_blank_lines_and_line_breaks_in_quotes()
    {
        var records = CsvRecords.Read("header\r\n\r\na,\"two\nlines\"\nb,c\n\n").Select(ResultOf).ToList();

        Assert.Equal([1, 3, 5], records.Select(record => record.Line));
        Assert.Equal("two\nlines", records[1].Fields[1]);
    }

    [Fact]
    public void A_quote_never_closed_is_reported_on_the_line_its_record_began()
    {
        var read = CsvRecords.Read("header\na,\"never closed\nb,c\n").ToList();

        Assert.Equal(2, read.Count);
        var defect = read[1].Match(_ => throw new Xunit.Sdk.XunitException("expected a defect"), problem => problem);
        Assert.Equal(2, defect.Line);
    }

    private static CsvRecord Single(string text) => ResultOf(Assert.Single(CsvRecords.Read(text)));

    private static CsvRecord ResultOf(Result<CsvRecord, LineDefect> result) =>
        result.Match(record => record, defect => throw new Xunit.Sdk.XunitException($"line {defect.Line} {defect.Reason}"));
}
