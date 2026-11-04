using lionheart.Model.Analysis;
using lionheart.Services.Oura;

namespace lionheart.Tests.Oura;

/// <summary>How the command line cuts a backfill into the months it pulls.</summary>
public class OuraSyncCommandTests
{
    [Fact]
    public void A_range_is_cut_at_month_boundaries_with_its_own_first_and_last_day_kept()
    {
        var months = OuraSyncCommand.Months(new DayRange(new DateOnly(2022, 1, 20), new DateOnly(2022, 3, 5)));

        Assert.Equal(
            [
                new DayRange(new DateOnly(2022, 1, 20), new DateOnly(2022, 1, 31)),
                new DayRange(new DateOnly(2022, 2, 1), new DateOnly(2022, 2, 28)),
                new DayRange(new DateOnly(2022, 3, 1), new DateOnly(2022, 3, 5)),
            ],
            months);
    }

    [Fact]
    public void A_range_inside_one_month_is_one_pull()
    {
        var range = new DayRange(new DateOnly(2023, 6, 3), new DateOnly(2023, 6, 3));

        Assert.Equal([range], OuraSyncCommand.Months(range));
    }

    [Fact]
    public void A_range_across_a_year_end_keeps_december_and_january_apart()
    {
        var months = OuraSyncCommand.Months(new DayRange(new DateOnly(2021, 12, 30), new DateOnly(2022, 1, 2)));

        Assert.Equal(
            [
                new DayRange(new DateOnly(2021, 12, 30), new DateOnly(2021, 12, 31)),
                new DayRange(new DateOnly(2022, 1, 1), new DateOnly(2022, 1, 2)),
            ],
            months);
    }
}
