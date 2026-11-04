using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Services.Cardio;
using lionheart.Model.Training.Score;
using lionheart.Services.Training.Score;
using static lionheart.Tests.Balance.ALog;

namespace lionheart.Tests.Balance;

/// <summary>
/// The score's arithmetic: earned only with every part met, no part paying for another, unknown
/// cardio left out rather than zeroed and never reading as excellent.
/// </summary>
public class TrainingScoreAnalysisTests
{
    private static readonly DateOnly Sunday = new(2026, 11, 8);

    private static readonly CardioMeasure FullCardio =
        new CardioMeasure.Measured(ScoreRules.Zone2.Committed, ScoreRules.Quality.Committed, 2, 3, 7);

    private static readonly CardioMeasure NoHeartRate = new CardioMeasure.Unknown(CardioUnknownReason.NoHeartRate);

    private static readonly CardioMeasure NoCardio = new CardioMeasure.Measured(0, 0, 0, 0, 7);

    /// <summary>The same cardio in every window, the score's own and the four its targets grow from.</summary>
    private static Func<DayRange, CardioMeasure> Every(CardioMeasure measure) => _ => measure;

    /// <summary>
    /// Three sessions in each of four weeks; the last week eight hard sets each of push, pull and
    /// legs at loads above the twelve weeks before.
    /// </summary>
    private static ALog AnExcellentMonth()
    {
        var log = Catalogue;
        foreach (var week in new[] { 21, 14, 7 })
        {
            log = log
                .WithSession(Sunday.AddDays(-week - 1), Set(Bench, 200, 5))
                .WithSession(Sunday.AddDays(-week - 3), Set(Row, 150, 8))
                .WithSession(Sunday.AddDays(-week - 5), Set(Squat, 300, 5));
        }

        return log
            .WithSession(Sunday.AddDays(-5), Sets(8, Bench, 210, 5))
            .WithSession(Sunday.AddDays(-3), Sets(8, Row, 160, 8))
            .WithSession(Sunday.AddDays(-1), Sets(8, Squat, 310, 5));
    }

    [Fact]
    public void A_week_meeting_every_target_with_new_bests_scores_a_hundred()
    {
        var score = TrainingScoreAnalysis.Score(AnExcellentMonth().Build(), [], Every(FullCardio), Sunday);

        Assert.Equal(100, score.Value);
        Assert.Empty(score.Unknown);
        Assert.Equal([3, 3, 3, 3], score.Consistency.Blocks.Select(block => block.Of.Sessions));
    }

    [Fact]
    public void Every_measurement_carries_its_own_points_and_together_they_are_the_parts()
    {
        var log = Catalogue.WithSession(Sunday, Sets(8, Bench)).WithSession(Sunday.AddDays(-2), Sets(2, Squat));
        var someCardio = new CardioMeasure.Measured(ScoreRules.Zone2.Committed / 4.0, 0, 0, 1, 7);

        var score = TrainingScoreAnalysis.Score(log.Build(), [], Every(someCardio), Sunday);

        // Nothing lifted before, so every family's target is the floor of four.
        var third = ScoreRules.LiftingPoints / 3.0;
        Assert.All(score.Lifting.Families, family => Assert.Equal(4, family.Of.Target.Target));
        Assert.Equal(
            [Math.Round(third, 6), 0, Math.Round(third * (0.5 * Math.Sqrt(2 / 4.0) + 0.5 * Math.Sqrt(2 / 8.0)), 6)],
            score.Lifting.Families.Select(family => Math.Round(family.Points, 6)));
        Assert.All(score.Lifting.Families, family => Assert.Equal(third, family.OutOf, precision: 6));
        Assert.Equal(score.Lifting.Points, score.Lifting.Families.Sum(family => family.Points), precision: 6);

        // 37.5 minutes a week before, grown ten per cent, is under the hour's floor.
        var bands = score.Cardio.Zones.Select(band => (band.Of.Band, band.Of.Target.Target, Math.Round(band.Points, 6), band.OutOf)).ToList();
        Assert.Equal(
            [(ZoneBand.Zone2, 60d, Math.Round(ScoreRules.Zone2Points * (0.5 * Math.Sqrt(37.5 / 60) + 0.5 * Math.Sqrt(37.5 / 150)), 6), (double)ScoreRules.Zone2Points),
             (ZoneBand.Quality, ScoreRules.Quality.Floor, 0d, ScoreRules.QualityPoints)],
            bands);
        Assert.Equal(score.Cardio.Points!.Value, score.Cardio.Zones.Sum(band => band.Points), precision: 6);

        Assert.All(score.Consistency.Blocks, block => Assert.Equal(ScoreRules.ConsistencyPoints / 4.0, block.OutOf));
        Assert.Equal(new DayRange(Sunday.AddDays(-6), Sunday), score.Consistency.Blocks[0].Of.Window);
    }

    [Fact]
    public void Unknown_cardio_has_no_bands_to_score()
    {
        var score = TrainingScoreAnalysis.Score(Catalogue.WithSession(Sunday, Sets(4, Bench)).Build(), [], Every(NoHeartRate), Sunday);

        Assert.Empty(score.Cardio.Zones);
    }

    [Fact]
    public void Over_doing_one_family_does_not_pay_for_another()
    {
        var log = Catalogue.WithSession(Sunday, Sets(24, Bench));

        var lifting = TrainingScoreAnalysis.Score(log.Build(), [], Every(FullCardio), Sunday).Lifting;

        Assert.Equal(ScoreRules.LiftingPoints / 3.0, lifting.Points, precision: 6);
    }

    [Fact]
    public void Unknown_cardio_is_named_and_its_points_are_out_of_reach_rather_than_zeroed()
    {
        var score = TrainingScoreAnalysis.Score(Catalogue.WithSession(Sunday, Sets(4, Bench)).Build(), [], Every(NoHeartRate), Sunday);

        Assert.Null(score.Cardio.Points);
        Assert.Equal([ScorePart.Cardio], score.Unknown);
        Assert.Equal(75, score.OutOf);
        var known = score.Lifting.Points + score.Progress.Points + score.Consistency.Points;
        Assert.Equal((int)Math.Round(known, MidpointRounding.AwayFromZero), score.Value);
    }

    [Fact]
    public void Taking_the_ring_off_never_scores_more_than_the_same_week_with_no_cardio_measured()
    {
        var worn = TrainingScoreAnalysis.Score(AnExcellentMonth().Build(), [], Every(NoCardio), Sunday);
        var off = TrainingScoreAnalysis.Score(AnExcellentMonth().Build(), [], Every(NoHeartRate), Sunday);

        Assert.Equal(worn.Value, off.Value);
        Assert.Equal((100, 75), (worn.OutOf, off.OutOf));
    }

    [Fact]
    public void Every_part_met_with_the_priorities_only_held_falls_short_of_ninety()
    {
        // The last week repeats the loads before it, so every priority is held rather than bettered.
        var held = Catalogue;
        foreach (var week in new[] { 21, 14, 7, 0 })
        {
            held = held
                .WithSession(Sunday.AddDays(-week - 1), Sets(8, Bench, 200, 5))
                .WithSession(Sunday.AddDays(-week - 3), Sets(8, Row, 150, 8))
                .WithSession(Sunday.AddDays(-week - 5), Sets(8, Squat, 300, 5));
        }

        var score = TrainingScoreAnalysis.Score(held.Build(), [], Every(FullCardio), Sunday);

        Assert.Equal(80 + ScoreRules.ProgressPoints * ScoreRules.HeldShare, score.Value);
        Assert.True(score.Value < 90);
    }

    [Theory]
    [InlineData(0, 8, 0)]
    [InlineData(2, 8, 0.5)]
    [InlineData(4, 8, 0.70710678)]
    [InlineData(8, 8, 1)]
    [InlineData(20, 8, 1)]
    public void A_measurement_pays_the_square_root_of_its_share_of_the_target_and_no_more(
        double measured, double target, double share) =>
        Assert.Equal(share, Progression.Share(measured, target), precision: 6);

    [Fact]
    public void Zero_measured_cardio_is_zero_points_not_unknown()
    {
        var score = TrainingScoreAnalysis.Score(AnExcellentMonth().Build(), [], Every(NoCardio), Sunday);

        Assert.Equal(0, score.Cardio.Points);
        Assert.Equal(75, score.Value);
    }

    [Fact]
    public void No_heart_rate_all_week_is_unknown_cardio() =>
        Assert.Equal(
            new CardioMeasure.Unknown(CardioUnknownReason.NoHeartRate),
            CardioMeasures.Of([], [], MaxHeartRate.Estimate(22, null), heartRateDays: 0));

    [Fact]
    public void Sessions_mostly_without_heart_rate_are_unknown_cardio_rather_than_low()
    {
        var start = new DateTimeOffset(2026, 11, 5, 18, 0, 0, TimeSpan.FromHours(-5));
        var unrecorded = new CardioSession(
            new CardioSessionSource.RingWorkout(Guid.NewGuid(), "running", WorkoutCategory.Endurance, RingEntry.Entered),
            start, start.AddHours(1), 8000, new SessionHeartRate.NotRecorded());

        Assert.Equal(
            new CardioMeasure.Unknown(CardioUnknownReason.SessionsUnrecorded),
            CardioMeasures.Of([unrecorded], [], MaxHeartRate.Estimate(22, null), heartRateDays: 6));
    }

    [Fact]
    public void Zone_two_and_quality_minutes_are_summed_across_the_sessions()
    {
        var start = new DateTimeOffset(2026, 11, 5, 18, 0, 0, TimeSpan.FromHours(-5));
        CardioSession Session(string activity, WorkoutCategory category, ZoneMinutes zones, params double[] bouts) => new(
            new CardioSessionSource.RingWorkout(Guid.NewGuid(), activity, category, RingEntry.Entered),
            start, start.AddHours(1), null,
            new SessionHeartRate.Recorded(140, 180, 1, zones, HeartRateSource.Ring)
            {
                Bouts = [.. bouts.Select(minutes => new WorkBout(start, minutes, 170))],
            });

        var measured = Assert.IsType<CardioMeasure.Measured>(CardioMeasures.Of(
            [
                Session("running", WorkoutCategory.Endurance, new ZoneMinutes(0, 5, 40, 10, 14, 1), 8, 6),
                Session("cycling", WorkoutCategory.Endurance, new ZoneMinutes(0, 0, 20, 0, 4, 0), 4),
                Session("basketball", WorkoutCategory.Sport, new ZoneMinutes(0, 0, 0, 0, 20, 0), 20),
            ],
            [],
            MaxHeartRate.Estimate(22, null),
            heartRateDays: 7));

        // The ride's four minutes are short of a session's worth, and a game earns none.
        Assert.Equal((60d, 14d, 1, 3), (measured.Zone2Minutes, measured.QualityMinutes, measured.QualitySessions, measured.Sessions));
    }

    [Fact]
    public void A_week_with_the_ring_off_is_measured_from_the_heart_rate_a_watch_recorded()
    {
        var start = new DateTimeOffset(2026, 11, 5, 18, 0, 0, TimeSpan.FromHours(-5));
        var run = new CardioSession(
            new CardioSessionSource.LoggedSession(Guid.NewGuid(), true, true), start, start.AddMinutes(40), 8000,
            new SessionHeartRate.Recorded(150, 175, 1, new ZoneMinutes(0, 0, 30, 10, 0, 0), HeartRateSource.AppleHealth));

        var measured = Assert.IsType<CardioMeasure.Measured>(CardioMeasures.Of([run], [], MaxHeartRate.Estimate(22, null), heartRateDays: 0));

        Assert.Equal(30, measured.Zone2Minutes);
    }

    [Fact]
    public void An_unread_walk_the_ring_guessed_at_does_not_make_a_week_of_real_data_unknown()
    {
        var start = new DateTimeOffset(2026, 11, 5, 18, 0, 0, TimeSpan.FromHours(-5));
        var run = new CardioSession(
            new CardioSessionSource.LoggedSession(Guid.NewGuid(), true, true), start, start.AddMinutes(30), 6000,
            new SessionHeartRate.Recorded(150, 175, 1, new ZoneMinutes(0, 0, 30, 0, 0, 0), HeartRateSource.AppleHealth));
        var walk = new CardioSession(
            new CardioSessionSource.RingWorkout(Guid.NewGuid(), "walking", WorkoutCategory.Walk, RingEntry.Detected),
            start.AddDays(1), start.AddDays(1).AddMinutes(60), null, new SessionHeartRate.NotRecorded());

        Assert.IsType<CardioMeasure.Measured>(CardioMeasures.Of([run], [walk], MaxHeartRate.Estimate(22, null), heartRateDays: 7));
    }

    // ---- Targets that follow capacity ----

    [Theory]
    [InlineData(new double[] { 5, 6, 7, 6 }, 7)] // a mean of six, grown ten per cent and rounded up
    [InlineData(new double[] { 0, 0, 0, 0 }, 4)] // after a lay-off, the floor
    [InlineData(new double[] { 10, 10, 10, 10 }, 8)] // never past the committed standard
    [InlineData(new double[] { 6 }, 7)] // only the blocks that could be read
    public void A_target_grows_ten_per_cent_from_the_four_weeks_before_between_floor_and_standard(double[] before, double target)
    {
        var weekly = Progression.Of(
            ScoreRules.FamilySets,
            [.. before.Select(sets => (double?)sets), .. Enumerable.Repeat<double?>(null, 4 - before.Length)]);

        Assert.Equal(target, weekly.Target);
        Assert.Equal(before.Average(), weekly.Chronic);
    }

    [Fact]
    public void A_target_rounds_up_to_its_step_without_a_floating_point_extra_step() =>
        Assert.Equal(110, Progression.Of(ScoreRules.Zone2, [100, 100, 100, 100]).Target);

    [Fact]
    public void With_no_week_before_that_could_be_read_the_target_is_the_committed_standard()
    {
        var weekly = Progression.Of(ScoreRules.Zone2, [null, null, null, null]);

        Assert.Equal((150d, (double?)null), (weekly.Target, weekly.Chronic));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 0.85355339)] // this week's target met: half whole, half the root of four of eight
    [InlineData(8, 1)] // only the committed standard pays the whole
    public void Half_of_a_measurement_pays_on_this_weeks_target_and_half_on_the_committed_standard(double sets, double share) =>
        Assert.Equal(share, Progression.Share(sets, new WeeklyTarget(4, 8, 4, 0)), precision: 6);

    [Fact]
    public void A_comeback_week_meeting_every_floor_reads_well_but_short_of_ninety()
    {
        // A first week back: four hard sets a family over three sessions, an hour of zone 2 and
        // one ten-minute quality session, after a month of nothing.
        var log = Catalogue
            .WithSession(Sunday.AddDays(-5), Sets(4, Bench, 200, 5))
            .WithSession(Sunday.AddDays(-3), Sets(4, Row, 150, 8))
            .WithSession(Sunday.AddDays(-1), Sets(4, Squat, 300, 5));
        var window = new DayRange(Sunday.AddDays(-6), Sunday);
        var comeback = new CardioMeasure.Measured(ScoreRules.Zone2.Floor, ScoreRules.Quality.Floor, 1, 2, 7);

        var score = TrainingScoreAnalysis.Score(log.Build(), [], range => range == window ? comeback : NoCardio, Sunday);

        Assert.All(score.Lifting.Families, family => Assert.Equal(4, family.Of.Target.Target));
        Assert.All(score.Cardio.Zones, zone => Assert.Equal(zone.Of.Minutes, zone.Of.Target.Target));
        Assert.InRange(score.Value, 50, 75);
        Assert.Equal(100, TrainingScoreAnalysis.Score(AnExcellentMonth().Build(), [], Every(FullCardio), Sunday).Value);
    }

    [Fact]
    public void Four_weeks_of_five_sets_set_a_target_of_six()
    {
        var log = Catalogue;
        foreach (var week in new[] { 7, 14, 21, 28 }) log = log.WithSession(Sunday.AddDays(-week), Sets(5, Bench));

        var push = TrainingScoreAnalysis.Score(log.Build(), [], Every(FullCardio), Sunday).Lifting.Families[0].Of;

        Assert.Equal((6d, (double?)5), (push.Target.Target, push.Target.Chronic));
    }

    [Fact]
    public void A_session_of_only_walking_is_not_a_session_trained()
    {
        var log = Catalogue.WithWalk(Sunday).WithWalk(Sunday.AddDays(-1)).WithSession(Sunday.AddDays(-2), Sets(3, Bench));

        var score = TrainingScoreAnalysis.Score(log.Build(), [], Every(FullCardio), Sunday);

        Assert.Equal(1, score.Consistency.Blocks[0].Of.Sessions);
    }

    [Fact]
    public void A_week_mostly_of_workouts_whose_heart_rate_could_not_be_read_is_unknown_rather_than_empty()
    {
        var start = new DateTimeOffset(2026, 11, 5, 18, 0, 0, TimeSpan.FromHours(-5));
        var unread = new CardioSession(
            new CardioSessionSource.RingWorkout(Guid.NewGuid(), "running", WorkoutCategory.Endurance, RingEntry.Detected),
            start, start.AddHours(1), null, new SessionHeartRate.NotRecorded());

        Assert.Equal(
            new CardioMeasure.Unknown(CardioUnknownReason.SessionsUnrecorded),
            CardioMeasures.Of([], [unread], MaxHeartRate.Estimate(22, null), heartRateDays: 6));
    }
}
