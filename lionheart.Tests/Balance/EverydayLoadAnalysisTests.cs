using lionheart.Model.Analysis;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Training.Load;
using lionheart.Services.Training.Load;

namespace lionheart.Tests.Balance;

/// <summary>
/// Everyday load: the ring's active slots outside training, weighted by band, judged against his
/// own last 90 complete days so an ordinary active day moves nothing.
/// </summary>
public class EverydayLoadAnalysisTests
{
    private static readonly DateOnly Today = new(2026, 11, 4);
    private static readonly TimeSpan Offset = TimeSpan.FromHours(-5);

    /// <summary>An Oura day from 04:00: the slots given, then rest to the end of the day.</summary>
    private static ActivityDay Day(DateOnly day, string slots, int nonWearSeconds = 0) =>
        new(day, new ActivityTimeline(Start(day), slots.PadRight(288, '1')), nonWearSeconds);

    private static DateTimeOffset Start(DateOnly day) => new(day.ToDateTime(new TimeOnly(4, 0)), Offset);

    /// <summary>A day of the given MET-minutes, in low-activity slots of 3.75 each.</summary>
    private static ActivityDay Of(DateOnly day, double metMinutes, int nonWearSeconds = 0) =>
        Day(day, new string('3', (int)(metMinutes / 3.75)), nonWearSeconds);

    /// <summary>Thirty complete days of 150 MET-minutes before the day.</summary>
    private static IEnumerable<ActivityDay> AMonthOfUsualDays(DateOnly before) =>
        Enumerable.Range(1, 30).Select(back => Of(before.AddDays(-back), 150));

    private static EverydayDay Judge(DateOnly day, IEnumerable<ActivityDay> days, IReadOnlyList<TrainingSpan>? training = null) =>
        Assert.Single(EverydayLoadAnalysis.Of([.. days], training ?? [], new DayRange(day, day), Today).Days);

    [Fact]
    public void Each_active_slot_counts_by_its_band_three_quarters_three_and_a_half_and_seven_met_minutes_a_minute()
    {
        // An hour each of low, medium and high: 45 + 210 + 420.
        var day = Day(Today, new string('3', 12) + new string('4', 12) + new string('5', 12));

        Assert.Equal(675, EverydayLoadAnalysis.MetMinutes(day.Timeline, []));
    }

    [Fact]
    public void Slots_a_training_session_covered_are_training_not_everyday_load()
    {
        var day = Day(Today, new string('5', 12));
        var session = new TrainingSpan(Start(Today).AddMinutes(30), Start(Today).AddMinutes(60));

        // Six of the twelve high slots fall inside the half hour of training.
        Assert.Equal(210, EverydayLoadAnalysis.MetMinutes(day.Timeline, [session]));
    }

    [Theory]
    [InlineData(150, EverydayBand.Usual)]
    [InlineData(165, EverydayBand.Above)]
    [InlineData(195, EverydayBand.FarAbove)]
    public void A_day_is_above_his_usual_over_his_ninetieth_percentile_and_far_above_a_fifth_over_that(double metMinutes, EverydayBand band)
    {
        var judged = Assert.IsType<EverydayStanding.Judged>(Judge(Today.AddDays(-1), [.. AMonthOfUsualDays(Today.AddDays(-1)), Of(Today.AddDays(-1), metMinutes)]).Standing);

        Assert.Equal((150d, 150d, band), (judged.Usual, judged.High, judged.Band));
    }

    [Fact]
    public void Fewer_than_four_weeks_of_complete_days_cannot_say_what_usual_is()
    {
        var day = Today.AddDays(-1);
        var days = AMonthOfUsualDays(day).Take(27).Append(Of(day, 600));

        Assert.IsType<EverydayStanding.Unjudged>(Judge(day, days).Standing);
    }

    [Fact]
    public void A_day_the_ring_was_off_for_more_than_three_hours_does_not_build_his_usual()
    {
        var day = Today.AddDays(-1);
        // Thirty quiet days with the ring off half of each would drag the usual down to nothing.
        var days = AMonthOfUsualDays(day)
            .Concat(Enumerable.Range(31, 30).Select(back => Of(day.AddDays(-back), 15, nonWearSeconds: 12 * 3600)))
            .Append(Of(day, 150));

        Assert.Equal(150, Assert.IsType<EverydayStanding.Judged>(Judge(day, days).Standing).Usual);
    }

    [Fact]
    public void Today_is_judged_but_builds_no_one_elses_usual_while_it_is_still_going()
    {
        var days = AMonthOfUsualDays(Today).Take(27).Append(Of(Today, 150)).ToList();

        var tomorrow = EverydayLoadAnalysis.Of(
            [.. days, Of(Today.AddDays(1), 150)], [], new DayRange(Today.AddDays(1), Today.AddDays(1)), Today);

        // Twenty-seven complete days and today: still short of the twenty-eight a usual needs.
        Assert.IsType<EverydayStanding.Unjudged>(Assert.Single(tomorrow.Days).Standing);
    }
}
