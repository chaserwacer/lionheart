using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Training.Load;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Score;
using lionheart.Services.Training.Load;
using static lionheart.Tests.Balance.ALog;

namespace lionheart.Tests.Balance;

/// <summary>
/// One definition of load — a week against the four before it — and what it does: a spike holds the
/// next week's target back, and a run of building weeks suggests a deload, which a lighter week
/// then reads as following.
/// </summary>
public class LoadAnalysisTests
{
    /// <summary>A Monday: the deload and the advice are decided from the Sundays before it.</summary>
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private static readonly Func<DayRange, CardioMeasure> NoCardio = _ => new CardioMeasure.Unknown(CardioUnknownReason.NoHeartRate);

    /// <summary>Push, pull and legs each trained once in the week ending <paramref name="weeksBack"/> Sundays before the Monday.</summary>
    private static ALog Week(ALog log, int weeksBack, int setsEach)
    {
        var sunday = Monday.AddDays(-1 - 7 * (weeksBack - 1));
        return log
            .WithSession(sunday.AddDays(-5), Sets(setsEach, Bench))
            .WithSession(sunday.AddDays(-3), Sets(setsEach, Row))
            .WithSession(sunday.AddDays(-1), Sets(setsEach, Squat));
    }

    /// <summary>Back from a lay-off, building 3, 6, 9, 12 then 15 sets a family: four building weeks, each well over the four before.</summary>
    private static ALog ASteepBuild() =>
        new[] { (5, 3), (4, 6), (3, 9), (2, 12), (1, 15) }.Aggregate(Catalogue, (log, week) => Week(log, week.Item1, week.Item2));

    /// <summary>Ten sets a family for weeks, one light week five weeks back, then four more of ten: building, but not steeply.</summary>
    private static ALog FourSteadyWeeksAfterALightOne() =>
        Enumerable.Range(1, 12).Aggregate(Catalogue, (log, back) => Week(log, back, back is 5 ? 2 : 10));

    private static Deload DeloadFor(ALog log, DateOnly day)
    {
        var built = log.Build();
        return LoadAnalysis.Deload(built, HardSetCounts.Of(built), day);
    }

    // ---- The ratio ----

    [Theory]
    [InlineData(12, 10, RampStanding.Within)]
    [InlineData(13, 10, RampStanding.Within)]   // 1.3, but a rise of three is under the noise of six
    [InlineData(26, 20, RampStanding.Steep)]
    [InlineData(30, 20, RampStanding.Spike)]
    [InlineData(10, 0, RampStanding.Spike)]     // a real amount after four weeks of nothing
    public void A_week_reads_steep_at_1_3_times_the_four_before_and_a_spike_at_1_5_once_the_rise_is_more_than_noise(
        double acute, double chronic, RampStanding standing) =>
        Assert.Equal(standing, new LoadRatio.Compared(acute, chronic).Standing(LoadAnalysis.LeastHardSetIncrease));

    [Fact]
    public void A_week_with_nothing_read_before_it_is_not_judged() =>
        Assert.Equal(RampStanding.Unjudged, LoadRatio.Of(40, [null, null, null, null]).Standing(0));

    [Fact]
    public void Cardio_rising_from_under_an_hour_a_week_is_a_start_rather_than_a_spike() =>
        Assert.Equal(
            RampStanding.Unjudged,
            new LoadRatio.Compared(200, 20).Standing(LoadAnalysis.LeastCardioMinutesIncrease, LoadAnalysis.LeastCardioMinutesChronic));

    // ---- Targets after a spike ----

    [Fact]
    public void A_steep_week_holds_the_next_target_at_last_weeks_rather_than_growing_on_it()
    {
        // Last week 130 against a usual 100: the ramp alone would ask 110 × 1.1 → 125.
        var target = Progression.Of(ScoreRules.Zone2, [130, 100, 100, 100, 100]);

        Assert.Equal(110, target.Target);
        var held = Assert.IsType<TargetGrowth.HeldAfterSteepWeek>(target.Growth);
        Assert.Equal(1.3, held.LastWeek.Ratio, precision: 6);
    }

    [Fact]
    public void A_spike_lowers_the_next_target_to_the_usual_of_the_weeks_before_it()
    {
        var target = Progression.Of(ScoreRules.Zone2, [160, 80, 80, 80, 80]);

        Assert.Equal(80, target.Target);
        Assert.IsType<TargetGrowth.LoweredAfterSpike>(target.Growth);
    }

    [Fact]
    public void A_spike_that_would_not_lower_a_target_already_at_the_committed_standard_leaves_it_ramped()
    {
        var target = Progression.Of(ScoreRules.FamilySets, [20, 12, 12, 12, 12]);

        Assert.Equal(ScoreRules.FamilySets.Committed, target.Target);
        Assert.IsType<TargetGrowth.Ramped>(target.Growth);
    }

    // ---- Deload ----

    [Fact]
    public void Four_building_weeks_of_a_steep_build_suggest_a_deload_with_their_reasons()
    {
        var deload = Assert.IsType<Deload.Suggested>(DeloadFor(ASteepBuild(), Monday));

        Assert.Equal((Monday, 4), (deload.WeekStarting, deload.BuildingWeeks));
        Assert.Contains(deload.Reasons, reason => reason.StartsWith("A steep build"));
    }

    [Fact]
    public void Three_building_weeks_do_not_suggest_a_deload()
    {
        var log = new[] { (4, 3), (3, 9), (2, 12), (1, 15) }.Aggregate(Catalogue, (built, week) => Week(built, week.Item1, week.Item2));

        Assert.Equal(3, Assert.IsType<Deload.NotSuggested>(DeloadFor(log, Monday)).BuildingWeeks);
    }

    [Fact]
    public void Four_steady_building_weeks_wait_for_tiredness_before_suggesting_a_deload()
    {
        Assert.Equal(4, Assert.IsType<Deload.NotSuggested>(DeloadFor(FourSteadyWeeksAfterALightOne(), Monday)).BuildingWeeks);

        var tired = FourSteadyWeeksAfterALightOne().WithRatedSession(Monday.AddDays(-2), fatigue: 4, difficulty: null, Set(Curl));

        var deload = Assert.IsType<Deload.Suggested>(DeloadFor(tired, Monday));
        Assert.Contains("Accumulated fatigue rated 4 of 5 on average last week.", deload.Reasons);
    }

    [Fact]
    public void Six_building_weeks_suggest_a_deload_whatever_the_ratings()
    {
        var log = Enumerable.Range(1, 8).Aggregate(Catalogue, (built, back) => Week(built, back, back is 8 ? 2 : 10));

        Assert.IsType<Deload.Suggested>(DeloadFor(log, Monday));
    }

    [Fact]
    public void A_lighter_week_ends_the_run_so_nothing_has_accumulated()
    {
        var log = new[] { (6, 3), (5, 6), (4, 9), (3, 12), (2, 15), (1, 2) }
            .Aggregate(Catalogue, (built, week) => Week(built, week.Item1, week.Item2));

        var deload = Assert.IsType<Deload.NotSuggested>(DeloadFor(log, Monday));
        Assert.Equal(0, deload.BuildingWeeks);
    }

    [Fact]
    public void A_week_within_seventy_per_cent_of_his_usual_reads_as_following_the_deload_and_one_over_it_as_training_through()
    {
        // The four weeks before the Monday held 36, 27, 18 and 45 hard sets: a usual of 31.5, a ceiling of 22.
        var light = ASteepBuild().WithSession(Monday.AddDays(2), Sets(20, Bench));
        var heavy = ASteepBuild().WithSession(Monday.AddDays(2), Sets(30, Bench));

        var following = Assert.IsType<Deload.Suggested>(DeloadFor(light, Monday.AddDays(3)));
        var through = Assert.IsType<Deload.Suggested>(DeloadFor(heavy, Monday.AddDays(3)));

        Assert.Equal((22d, 20, true), (following.Ceiling, following.SoFar, following.Following));
        Assert.False(through.Following);
    }

    [Fact]
    public void The_deload_is_decided_from_the_weeks_before_the_monday_so_it_holds_all_week()
    {
        var log = ASteepBuild().WithSession(Monday.AddDays(2), Sets(30, Bench));

        Assert.All(
            Enumerable.Range(0, 7).Select(day => DeloadFor(log, Monday.AddDays(day))),
            deload => Assert.Equal(Monday, Assert.IsType<Deload.Suggested>(deload).WeekStarting));
    }

    // ---- Cardio and everyday ----

    [Fact]
    public void Running_far_over_the_four_weeks_before_reads_as_a_spike_while_its_minutes_stay_within()
    {
        var window = new DayRange(Monday.AddDays(-7), Monday.AddDays(-1));
        CardioMeasure Of(DayRange range) => new CardioMeasure.Measured(100, 0, 0, 3, 7)
        {
            Minutes = 200,
            RunningMeters = range == window ? 30_000 : 15_000,
        };

        var cardio = Assert.IsType<CardioLoad.Read>(LoadAnalysis.Cardio(Of, window));

        Assert.Equal((RampStanding.Spike, RampStanding.Within), (cardio.RunningMeters.Standing, cardio.Minutes.Standing));
    }

    [Fact]
    public void Two_days_far_above_his_usual_ease_the_week_by_half_a_day_each_and_one_eases_nothing()
    {
        var window = new DayRange(Monday.AddDays(-7), Monday.AddDays(-1));
        EverydayDay Day(int offset, EverydayBand band) =>
            new(window.From.AddDays(offset), 900, new EverydayStanding.Judged(500, 700, band));

        var one = LoadAnalysis.Everyday(new EverydayLoad([Day(0, EverydayBand.FarAbove), Day(1, EverydayBand.Above)]), window);
        var two = LoadAnalysis.Everyday(new EverydayLoad([Day(0, EverydayBand.FarAbove), Day(1, EverydayBand.FarAbove)]), window);

        Assert.Equal((0d, 1d), (one.EasedDays, two.EasedDays));
    }
}
