using lionheart.Model.Cardio;
using lionheart.Model.Training.Balance;
using lionheart.Model.Training.GoingIn;
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
        IReadOnlyList<PlannedFact>? planned = null) =>
        new(Evening,
            sessions ?? [],
            cardio ?? new CardioDays.Read([], null),
            load ?? new LoadFact(10, 10, 2, 2),
            nights ?? [],
            baseline ?? new NightBaseline(70, 45, 40),
            readiness,
            new WeekMeans(null, null, 0),
            wellness,
            [],
            planned ?? [],
            string.Empty);

    private static GoingInState Of(TodayFacts facts, IReadOnlyList<MuscleVolume>? muscles = null) =>
        GoingInAnalysis.Of(facts, muscles ?? [], []);

    private static int ScoreOf(PartScore part) => Assert.IsType<PartScore.Scored>(part).Score;

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
    public void A_week_well_above_the_months_weekly_mean_costs_load()
    {
        // 26 over a mean of 10 is 2.6, 1.3 over the allowance: 100 - 65.
        var state = Of(Facts(load: new LoadFact(26, 10, 4, 2)));

        Assert.Equal(35, ScoreOf(state.Load.Score));
    }

    [Fact]
    public void Cardio_that_could_not_be_read_leaves_its_minutes_absent_rather_than_zero()
    {
        var state = Of(Facts(cardio: new CardioDays.Unread()));

        Assert.Null(state.Load.HighIntensityMinutes);
        Assert.Null(state.Load.ZoneTwoMinutes);
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
        var state = Of(Facts(nights: [Night(Today)], readiness: Readiness(85), load: new LoadFact(10, 10, 2, 2)));

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
