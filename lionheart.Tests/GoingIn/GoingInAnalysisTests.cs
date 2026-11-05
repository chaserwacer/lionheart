using lionheart.Model.Cardio;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.GoingIn;
using lionheart.Model.Training.Load;
using lionheart.Model.Training.Patterns;
using lionheart.Services.Chat.Today;
using lionheart.Services.Training.GoingIn;

namespace lionheart.Tests.GoingIn;

/// <summary>
/// The going-in rating from facts built by hand. The claims: each part scores what the info sheet
/// says it scores, the rating is the mean of the parts that could be scored and no other, and a day
/// with nothing about the body to read is not rated at all.
/// </summary>
public sealed class GoingInAnalysisTests
{
    private static readonly AthleteNow Evening = new(new DateTimeOffset(2026, 3, 10, 18, 30, 0, TimeSpan.FromHours(-5)));
    private static readonly DateOnly Today = Evening.Today;
    private static readonly DateOnly Yesterday = Evening.Yesterday;

    private static NightFact Night(DateOnly day, int? hrv = 70, int? lowest = 45, int? score = 80) =>
        new(day,
            new DateTimeOffset(day.AddDays(-1).ToDateTime(new TimeOnly(23, 0)), TimeSpan.FromHours(-5)),
            new DateTimeOffset(day.ToDateTime(new TimeOnly(7, 0)), TimeSpan.FromHours(-5)),
            28_000, 90, lowest, hrv, score);

    private static ReadinessFact Readiness(int score) => new(score, 80, 75, 0.1, 65, 88);

    private static SessionFact Lifting(DateOnly day, int hardSets) =>
        new(day, new DateTimeOffset(day.ToDateTime(new TimeOnly(18, 0)), TimeSpan.FromHours(-5)), TimeSpan.FromMinutes(60),
            [new LiftFact("Bench Press", PatternFamily.Push, hardSets, "100 lb × 5")], false, null, null);

    private static TodayFacts Facts(
        IReadOnlyList<NightFact>? nights = null,
        ReadinessFact? readiness = null,
        NightBaseline? baseline = null,
        IReadOnlyList<SessionFact>? sessions = null,
        CardioDays? cardio = null,
        LoadFact? load = null,
        WellnessFact? wellness = null,
        IReadOnlyList<PlannedFact>? planned = null,
        EverydayLoad? everyday = null) =>
        new(Evening,
            sessions ?? [],
            cardio ?? new CardioDays.Read([], null),
            load ?? Load(10, 10),
            nights ?? [],
            baseline ?? new NightBaseline(70, 45, 40),
            readiness,
            new WeekMeans(null, null, 0),
            wellness,
            [],
            planned ?? [],
            [],
            everyday ?? EverydayLoad.None);

    private static LoadFact Load(int hardSets, double chronic) =>
        new(new LoadRatio.Compared(hardSets, chronic), new LoadRatio.Compared(2, 2));

    private static GoingInState Of(TodayFacts facts, IReadOnlyList<MuscleVolume>? muscles = null) =>
        GoingInAnalysis.Of(facts, muscles ?? [], []);

    private static int ScoreOf(PartScore part) => Assert.IsType<PartScore.Scored>(part).Score;

    [Fact]
    public void Everyday_load_costs_the_load_part_only_for_what_ran_over_his_usual_high()
    {
        // Yesterday 100 MET-minutes over his high: 3 points. Today at his usual: nothing.
        var everyday = new EverydayLoad(
        [
            new EverydayDay(Yesterday, 900, new EverydayStanding.Judged(600, 800, EverydayBand.FarAbove)),
            new EverydayDay(Today, 600, new EverydayStanding.Judged(600, 800, EverydayBand.Usual)),
        ]);

        var state = Of(Facts(everyday: everyday));

        Assert.Equal(97, ScoreOf(state.Load.Score));
        Assert.Equal(2, state.Load.Everyday.Count);
    }

    [Fact]
    public void A_usual_night_scores_its_autonomic_part_at_the_usual_seventy_five()
    {
        var state = Of(Facts(nights: [Night(Today)]));

        Assert.Equal(75, ScoreOf(state.Autonomic.Score));
    }

    [Fact]
    public void Hrv_under_baseline_and_a_raised_lowest_heart_rate_each_cost_their_half()
    {
        // HRV 20 % under: 75 - 25 = 50. Lowest heart rate 3 over: 75 - 24 = 51. Mean 50.5.
        var state = Of(Facts(nights: [Night(Today, hrv: 56, lowest: 48)]));

        Assert.Equal(51, ScoreOf(state.Autonomic.Score));
        Assert.Equal(56, state.Autonomic.Hrv);
        Assert.Equal(70, state.Autonomic.HrvBaseline);
    }

    [Fact]
    public void A_baseline_of_too_few_nights_scores_nothing_rather_than_comparing_with_noise()
    {
        var state = Of(Facts(nights: [Night(Today)], baseline: new NightBaseline(70, 45, GoingInAnalysis.FewestBaselineNights - 1)));

        Assert.IsType<PartScore.NotScored>(state.Autonomic.Score);
    }

    [Fact]
    public void Sleep_weighs_last_night_two_to_one_over_the_night_before()
    {
        var state = Of(Facts(nights: [Night(Today, score: 90), Night(Yesterday, score: 60)]));

        Assert.Equal(80, ScoreOf(state.Sleep.Score));
        Assert.Equal(Today, state.Sleep.LastNight?.Day);
        Assert.Equal(Yesterday, state.Sleep.NightBefore?.Day);
    }

    [Fact]
    public void A_night_not_synced_yet_leaves_sleep_and_autonomic_unscored_and_the_night_before_still_shown()
    {
        var state = Of(Facts(nights: [Night(Yesterday)], readiness: Readiness(80)));

        Assert.IsType<PartScore.NotScored>(state.Sleep.Score);
        Assert.IsType<PartScore.NotScored>(state.Autonomic.Score);
        Assert.Null(state.Sleep.LastNight);
        Assert.Equal(Yesterday, state.Sleep.NightBefore?.Day);
    }

    [Fact]
    public void Load_counts_hard_sets_since_yesterday_and_cardio_minutes_and_not_the_days_before()
    {
        var state = Of(Facts(
            sessions: [Lifting(Yesterday.AddDays(-1), 30), Lifting(Yesterday, 12)],
            cardio: new CardioDays.Read(
                [new CardioFact(Today, Evening.Local.AddHours(-10), 40, "running", 150, ZoneTwo: 20, ZoneFourPlus: 6)], null)));

        // 100 - 18 - 6 - 3
        Assert.Equal(73, ScoreOf(state.Load.Score));
        Assert.Equal(12, state.Load.HardSets);
        Assert.Equal(6, state.Load.HighIntensityMinutes);
        Assert.Equal(20, state.Load.ZoneTwoMinutes);
    }

    [Fact]
    public void A_week_well_above_the_four_weeks_before_costs_load()
    {
        // 49 over a mean of 20 is 2.45, one over the allowance: 100 - 30.
        var state = Of(Facts(load: Load(49, 20)));

        Assert.Equal(70, ScoreOf(state.Load.Score));
    }

    [Fact]
    public void A_steep_week_short_of_the_allowance_costs_no_load()
    {
        // 14 over 10 is 1.4: steep for the ramp, still inside what this morning's readiness allows.
        var state = Of(Facts(load: Load(14, 10)));

        Assert.Equal(100, ScoreOf(state.Load.Score));
    }

    [Fact]
    public void Cardio_that_could_not_be_read_leaves_load_unscored_and_its_minutes_absent_rather_than_zero()
    {
        var state = Of(Facts(nights: [Night(Today)], cardio: new CardioDays.Unread(), sessions: [Lifting(Yesterday, 12)]));

        Assert.IsType<PartScore.NotScored>(state.Load.Score);
        Assert.Null(state.Load.HighIntensityMinutes);
        Assert.Null(state.Load.ZoneTwoMinutes);
        Assert.Equal(12, state.Load.HardSets);
        Assert.IsType<GoingInRating.Rated>(state.Rating);
    }

    [Fact]
    public void Cardio_with_no_heart_rate_to_zone_still_costs_load_by_its_minutes()
    {
        var state = Of(Facts(cardio: new CardioDays.Read(
            [new CardioFact(Yesterday, Evening.Local.AddDays(-1), 40, "running", null, ZoneTwo: null, ZoneFourPlus: null)], null)));

        // 100 - 0.5 × 40
        Assert.Equal(80, ScoreOf(state.Load.Score));
    }

    [Fact]
    public void Sleep_alone_makes_a_rating_and_carries_the_whole_of_it_with_load()
    {
        var state = Of(Facts(nights: [Night(Today, hrv: null, lowest: null, score: 70)]));

        // Sleep 70 × .20 and load 100 × .15, over .35.
        var rated = Assert.IsType<GoingInRating.Rated>(state.Rating);
        Assert.Equal(83, rated.Score);
        Assert.IsType<PartScore.NotScored>(state.Readiness.Score);
        Assert.IsType<PartScore.NotScored>(state.Autonomic.Score);
    }

    [Fact]
    public void Autonomic_with_one_half_missing_is_the_half_it_has()
    {
        var state = Of(Facts(nights: [Night(Today, hrv: null, lowest: 47)]));

        // Lowest heart rate 2 over: 75 - 16.
        Assert.Equal(59, ScoreOf(state.Autonomic.Score));
    }

    [Fact]
    public void Hrv_far_above_baseline_is_held_at_one_hundred()
    {
        var state = Of(Facts(nights: [Night(Today, hrv: 140, lowest: 45)]));

        // HRV half clamps at 100, heart-rate half 75.
        Assert.Equal(88, ScoreOf(state.Autonomic.Score));
    }

    [Fact]
    public void Wellness_rated_on_an_earlier_day_is_not_todays()
    {
        var earlier = Of(Facts(wellness: new WellnessFact(Yesterday, 4, 4, 4, 2, 4)));
        var today = Of(Facts(wellness: new WellnessFact(Today, 4, 4, 4, 2, 4)));

        Assert.IsType<PartScore.NotScored>(earlier.Wellness.Score);
        Assert.Null(earlier.Wellness.Today);
        Assert.Equal(75, ScoreOf(today.Wellness.Score));
    }

    [Fact]
    public void The_rating_is_the_weighted_mean_of_the_scored_parts_and_their_shares_sum_to_one()
    {
        var state = Of(Facts(nights: [Night(Today)], readiness: Readiness(85), load: Load(10, 10)));

        // readiness 85 × .30, autonomic 75 × .25, sleep 80 × .20, load 100 × .15, over .90.
        var rated = Assert.IsType<GoingInRating.Rated>(state.Rating);
        Assert.Equal(84, rated.Score);
        Assert.Equal(GoingInBand.High, rated.Band);

        double[] shares = [.. new[] { state.Readiness.Score, state.Autonomic.Score, state.Sleep.Score, state.Load.Score, state.Wellness.Score }
            .OfType<PartScore.Scored>().Select(part => part.Share)];
        Assert.Equal(1, shares.Sum(), 6);
        Assert.Equal(GoingInAnalysis.ReadinessWeight / 0.9, ((PartScore.Scored)state.Readiness.Score).Share, 6);
    }

    [Fact]
    public void With_nothing_about_the_body_to_read_there_is_no_rating_though_load_is_scored()
    {
        var state = Of(Facts(wellness: new WellnessFact(Today, 4, 4, 4, 2, 4)));

        Assert.IsType<GoingInRating.NotRated>(state.Rating);
        Assert.IsType<PartScore.Scored>(state.Load.Score);
    }

    [Theory]
    [InlineData(80, GoingInBand.High)]
    [InlineData(79, GoingInBand.Normal)]
    [InlineData(65, GoingInBand.Normal)]
    [InlineData(64, GoingInBand.Reduced)]
    [InlineData(50, GoingInBand.Reduced)]
    [InlineData(49, GoingInBand.Low)]
    public void Bands_fall_at_eighty_sixty_five_and_fifty(int score, GoingInBand band) =>
        Assert.Equal(band, GoingInAnalysis.BandOf(score));

    [Fact]
    public void Only_todays_plans_and_the_muscles_trained_since_yesterday_are_noted()
    {
        var state = Of(
            Facts(planned: [new PlannedFact(Today, ["Squat"]), new PlannedFact(Today.AddDays(1), ["Bench Press"])]),
            [
                new MuscleVolume(MuscleGroup.Quads, 6, 0, 4, Yesterday),
                new MuscleVolume(MuscleGroup.Chest, 4, 0, 4, Yesterday.AddDays(-1)),
                new MuscleVolume(MuscleGroup.Lats, 0, 0, 4, null),
            ]);

        Assert.Equal(["Squat"], Assert.Single(state.PlannedToday).Movements);
        Assert.Equal(MuscleGroup.Quads, Assert.Single(state.RecentMuscles).Muscle);
    }
}
