using lionheart.Model.Analysis;
using lionheart.Model.Habits;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Services.Habits;

namespace lionheart.Tests.Habits;

/// <summary>
/// The habits report's rules over hand-built activity days: what breaks a sitting, which days
/// count, and how sleep timing is averaged across midnight.
/// </summary>
public class HabitsAnalysisTests
{
    private static readonly Guid Athlete = Guid.NewGuid();
    private static readonly TimeSpan Eastern = TimeSpan.FromHours(-4);

    /// <summary>A Wednesday.</summary>
    private static readonly DateOnly Day = new(2026, 10, 14);

    // ---- Sittings ----

    [Fact]
    public void One_five_minute_slot_of_movement_ends_a_sitting()
    {
        // 04:00 start: 12 slots inactive (an hour), one low, then 8 inactive (40 minutes).
        var detail = Detail(Classes("222222222222" + "3" + "22222222"));

        Assert.Equal([60, 40], detail.SedentaryBouts.Select(bout => bout.Minutes));
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 5, 5, 0, Eastern), detail.SedentaryBouts[1].Start);
        Assert.Equal(60, detail.Figures.LongestSedentaryMinutes);
        Assert.Equal(1, detail.Figures.LongSedentaryBouts);
    }

    [Fact]
    public void A_still_stretch_Oura_files_as_rest_does_not_break_a_sitting()
    {
        var detail = Detail(Classes("2222" + "11" + "222222"));

        Assert.Equal(60, Assert.Single(detail.SedentaryBouts).Minutes);
    }

    [Fact]
    public void Half_an_hour_of_rest_outside_a_scored_sleep_is_lying_still_and_ends_a_sitting()
    {
        var detail = Detail(Classes("222222" + "111111" + "222222"));

        Assert.Equal([30, 30], detail.SedentaryBouts.Select(bout => bout.Minutes));
    }

    [Fact]
    public void A_day_of_nothing_but_unscored_rest_is_the_ring_on_a_table_and_does_not_count()
    {
        // In the athlete's archive: whole days of rest with zero steps, which read as a day-long
        // sitting until lying still is told apart from it.
        var detail = Detail(Activity(Day, new string('1', 288)));

        Assert.False(detail.Figures.IsComplete);
        Assert.Empty(detail.SedentaryBouts);
    }

    [Fact]
    public void Rest_inside_a_sleep_period_is_sleep_and_not_a_sitting()
    {
        var timeline = Classes("1111111111111111" + "22222222");
        var nap = Nap(Day.ToDateTime(new TimeOnly(4, 0)), minutes: 80);

        var detail = Detail(timeline, sleep: [nap]);

        Assert.Equal(40, Assert.Single(detail.SedentaryBouts).Minutes);
        Assert.True(detail.Runs[0].IsAsleep);
        Assert.Equal(80, detail.Runs[0].Minutes);
    }

    [Fact]
    public void A_stretch_shorter_than_half_an_hour_is_not_a_bout()
    {
        var detail = Detail(Classes("22222" + "3" + "22222"));

        Assert.Empty(detail.SedentaryBouts);
        Assert.Equal(0, detail.Figures.LongestSedentaryMinutes);
    }

    [Fact]
    public void Non_wear_breaks_a_sitting_since_the_ring_cannot_say_what_happened()
    {
        var detail = Detail(Classes("222222" + "0" + "222222"));

        Assert.Equal([30, 30], detail.SedentaryBouts.Select(bout => bout.Minutes));
    }

    [Fact]
    public void An_undocumented_digit_reads_as_unrecognised_and_breaks_nothing_else()
    {
        var detail = Detail(Classes("2222229222222"));

        Assert.Contains(detail.Runs, run => run.Class is ActivityClass.Unrecognised);
        Assert.Equal([30, 30], detail.SedentaryBouts.Select(bout => bout.Minutes));
    }

    // ---- Which days count ----

    [Fact]
    public void A_day_with_more_than_three_hours_off_the_finger_does_not_count_toward_a_summary()
    {
        var worn = Activity(Day, new string('2', 252) + new string('0', 36));
        var unworn = Activity(Day.AddDays(1), new string('2', 251) + new string('0', 37));

        var report = HabitsAnalysis.Build(new HabitsInputs(new DayRange(Day, Day.AddDays(1)), [worn, unworn], [], []));

        Assert.Equal([true, false], report.Days.Select(day => day.IsComplete));
        Assert.Equal(1, report.Current.AllDays.CompleteDays);
    }

    [Fact]
    public void A_day_stored_without_a_classification_is_listed_but_never_complete()
    {
        var bare = new OuraDailyActivity(Guid.NewGuid(), Athlete, Day, Reading(timeline: null));

        var report = HabitsAnalysis.Build(new HabitsInputs(new DayRange(Day, Day), [bare], [], []));

        var day = Assert.Single(report.Days);
        Assert.False(day.IsComplete);
        Assert.Null(day.LongestSedentaryMinutes);
        Assert.Null(report.Current.AllDays.Steps);
    }

    [Fact]
    public void The_previous_span_is_as_long_as_the_range_and_ends_the_day_before_it()
    {
        Assert.Equal(
            new DayRange(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)),
            HabitsAnalysis.PreviousOf(new DayRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 30))));
    }

    [Fact]
    public void The_hour_profile_is_the_mean_complete_day_on_the_athletes_own_clock()
    {
        // Both days: 04:00-05:00 inactive, then light movement to the end.
        var classes = new string('2', 12) + new string('3', 276);
        var report = HabitsAnalysis.Build(new HabitsInputs(
            new DayRange(Day, Day.AddDays(1)), [Activity(Day, classes), Activity(Day.AddDays(1), classes)], [], []));

        var hours = report.Current.AllDays.Hours;
        Assert.Equal(60, hours[4].Sedentary);
        Assert.Equal(60, hours[5].Low);
        Assert.Equal(0, hours[5].Sedentary);
    }

    // ---- Sleep timing ----

    [Fact]
    public void Bedtimes_either_side_of_midnight_average_to_midnight_not_to_noon()
    {
        var nights = new[]
        {
            Night(new DateTimeOffset(2026, 10, 13, 23, 30, 0, Eastern)),
            Night(new DateTimeOffset(2026, 10, 15, 0, 30, 0, Eastern)),
            Night(new DateTimeOffset(2026, 10, 15, 23, 30, 0, Eastern)),
            Night(new DateTimeOffset(2026, 10, 17, 0, 30, 0, Eastern)),
        };

        var timing = HabitsAnalysis.Timing(nights);

        Assert.NotNull(timing);
        Assert.Equal(720, timing.BedtimeMinutesAfterNoon, precision: 6);
        Assert.Equal(Math.Sqrt(4 * 30 * 30 / 3.0), timing.BedtimeSpreadMinutes, precision: 6);
        Assert.Equal(4, timing.Nights);
    }

    [Fact]
    public void Fewer_than_three_nights_is_not_a_sleep_timing()
    {
        Assert.Null(HabitsAnalysis.Timing([Night(new DateTimeOffset(2026, 10, 13, 23, 0, 0, Eastern))]));
    }

    // ---- Builders ----

    private static HabitsDayDetail Detail(OuraDailyActivity activity, IReadOnlyList<OuraSleepPeriod>? sleep = null) =>
        HabitsAnalysis.Detail(activity.Day, new HabitsInputs(new DayRange(activity.Day, activity.Day), [activity], sleep ?? [], []))
        ?? throw new InvalidOperationException("No detail for a day that has an activity document.");

    /// <summary>
    /// A day whose timeline begins with <paramref name="classes"/> and moves lightly for the rest of
    /// its 288 slots, so nothing after the part under test is a sitting.
    /// </summary>
    private static OuraDailyActivity Classes(string classes) => Activity(Day, classes.PadRight(288, '3'));

    private static OuraDailyActivity Activity(DateOnly day, string classes) =>
        new(Guid.NewGuid(), Athlete, day, Reading(new ActivityTimeline(
            new DateTimeOffset(day.ToDateTime(new TimeOnly(4, 0)), Eastern), classes)));

    private static DailyActivityReading Reading(ActivityTimeline? timeline) => new(
        80, new ActivityContributors(80, 80, 80, 80, 80, 80), 9000, 400, 2800, 450, 7000, 0, 9000, 0, 1.4,
        150, 100, 10, 10, 30000, 30000, 12000, 2000, 300, 0, timeline);

    private static OuraSleepPeriod Nap(DateTime localStart, int minutes)
    {
        var start = new DateTimeOffset(localStart, Eastern);
        return SleepPeriod(start, start.AddMinutes(minutes), "late_nap", DateOnly.FromDateTime(localStart));
    }

    private static OuraSleepPeriod Night(DateTimeOffset bedtime) =>
        SleepPeriod(bedtime, bedtime.AddHours(8), SleepPeriodReading.MainSleepKind, DateOnly.FromDateTime(bedtime.AddHours(8).DateTime));

    private static OuraSleepPeriod SleepPeriod(DateTimeOffset start, DateTimeOffset end, string kind, DateOnly day)
    {
        var empty = new OuraSampleSeries(300, [], start);
        return new OuraSleepPeriod(Guid.NewGuid(), Athlete, day, new SleepPeriodReading(
            new Interval(start, end), kind, 0, (int)(end - start).TotalSeconds, 25200, 5400, 14400, 5400, 3600,
            600, 88, 10, 50, 45, 80, 14, null, null, null, null, "4222", empty, empty));
    }
}
