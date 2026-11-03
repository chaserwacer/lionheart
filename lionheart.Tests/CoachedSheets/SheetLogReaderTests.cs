using lionheart.Functional;
using lionheart.Model.Training.Sets;
using lionheart.Services.CoachedSheets;
using lionheart.Tests.Support;
using static lionheart.Tests.CoachedSheets.SheetLogs;

namespace lionheart.Tests.CoachedSheets;

/// <summary>Reading training-os's set log for the programme-sheet sets it holds.</summary>
public class SheetLogReaderTests
{
    [Fact]
    public void A_sheet_row_reads_as_the_set_it_records()
    {
        using var log = new LogFile(
            Set("2023-12-19", "mk-2", 1, "Tuesday", "paused-ssb-squat", "Paused SSB Squat", 245, 6, rpe: "6.5", note: "prescribed RPE 6.5"));

        var set = Assert.Single(ResultAssert.Succeeded(SheetLogReader.Read(log.Path)).Sets);

        Assert.Equal(
            new SheetSet(2, "2023-12-19-mk-2-tuesday", new DateOnly(2023, 12, 19), "Tuesday", new SheetSource("mk-2", 1),
                "paused-ssb-squat", "Paused SSB Squat", 245, 6, Rpe.Of(6.5), "prescribed RPE 6.5"),
            set);
    }

    [Fact]
    public void Strong_rows_are_counted_and_not_read_as_sets()
    {
        using var log = new LogFile(StrongRow("2024-08-20"), StrongRow("2024-08-22"));

        var read = ResultAssert.Succeeded(SheetLogReader.Read(log.Path));

        Assert.Equal((0, 2), (read.Sets.Count, read.StrongRowsSetAside));
    }

    [Fact]
    public void Every_unreadable_line_is_named_and_nothing_is_read()
    {
        using var log = new LogFile(
            Set("2023-12-19", "mk-2", 1, "Tuesday", "squat-barbell", "Squat", 245, 6),
            Set("2023-12-19", "mk-2", 1, "Tuesday", "squat-barbell", "Squat", 0, 6),
            Set("2023-12-19", "mk-2", 1, "Tuesday", "squat-barbell", "Squat", 245, 6, rpe: "11"),
            Set("2023-12-19", "mk-2", 1, "Tuesday", "squat-barbell", "Squat", 245, 6).Replace("sheet:mk-2#w1", "note:2023-12-19"));

        var problem = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(SheetLogReader.Read(log.Path)));

        Assert.Equal(["line 3", "line 4", "line 5"], problem.Fields.Select(field => field.Field));
        Assert.Contains("is not a programme sheet's source", problem.Fields[2].Message);
    }

    [Fact]
    public void A_file_without_the_log_columns_is_refused_by_what_it_lacks()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sets-{Guid.NewGuid():N}.tsv");
        File.WriteAllLines(path, ["date\tweight\treps", "2023-12-19\t245\t6"]);
        try
        {
            var problem = ResultAssert.Failed(SheetLogReader.Read(path));

            Assert.Contains("not a training-os set log", problem.Description);
            Assert.Contains("'source'", problem.Description);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
