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
        new CardioMeasure.Measured(150, ScoreRules.Zone2Minutes, 20, ScoreRules.HighIntensityMinutes, 3, 7);

    private static readonly CardioMeasure NoHeartRate = new CardioMeasure.Unknown(CardioUnknownReason.NoHeartRate);

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
        var score = TrainingScoreAnalysis.Score(AnExcellentMonth().Build(), [], FullCardio, Sunday);

        Assert.Equal(100, score.Value);
        Assert.Empty(score.Unknown);
        Assert.Equal([3, 3, 3, 3], score.Consistency.SessionsPerBlock);
    }

    [Fact]
    public void Over_doing_one_family_does_not_pay_for_another()
    {
        var log = Catalogue.WithSession(Sunday, Sets(24, Bench));

        var lifting = TrainingScoreAnalysis.Score(log.Build(), [], FullCardio, Sunday).Lifting;

        Assert.Equal(ScoreRules.LiftingPoints / 3.0, lifting.Points, precision: 6);
    }

    [Fact]
    public void Unknown_cardio_is_named_and_its_points_are_out_of_reach_rather_than_zeroed()
    {
        var score = TrainingScoreAnalysis.Score(Catalogue.WithSession(Sunday, Sets(4, Bench)).Build(), [], NoHeartRate, Sunday);

        Assert.Null(score.Cardio.Points);
        Assert.Equal([ScorePart.Cardio], score.Unknown);
        Assert.Equal(75, score.OutOf);
        var known = score.Lifting.Points + score.Progress.Points + score.Consistency.Points;
        Assert.Equal((int)Math.Round(known, MidpointRounding.AwayFromZero), score.Value);
    }

    [Fact]
    public void Taking_the_ring_off_never_scores_more_than_the_same_week_with_no_cardio_measured()
    {
        var none = new CardioMeasure.Measured(0, ScoreRules.Zone2Minutes, 0, ScoreRules.HighIntensityMinutes, 0, 7);

        var worn = TrainingScoreAnalysis.Score(AnExcellentMonth().Build(), [], none, Sunday);
        var off = TrainingScoreAnalysis.Score(AnExcellentMonth().Build(), [], NoHeartRate, Sunday);

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

        var score = TrainingScoreAnalysis.Score(held.Build(), [], FullCardio, Sunday);

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
        Assert.Equal(share, TrainingScoreAnalysis.Share(measured, target), precision: 6);

    [Fact]
    public void Zero_measured_cardio_is_zero_points_not_unknown()
    {
        var none = new CardioMeasure.Measured(0, ScoreRules.Zone2Minutes, 0, ScoreRules.HighIntensityMinutes, 0, 7);

        var score = TrainingScoreAnalysis.Score(AnExcellentMonth().Build(), [], none, Sunday);

        Assert.Equal(0, score.Cardio.Points);
        Assert.Equal(75, score.Value);
    }

    [Fact]
    public void No_heart_rate_all_week_is_unknown_cardio() =>
        Assert.Equal(
            new CardioMeasure.Unknown(CardioUnknownReason.NoHeartRate),
            CardioMeasures.Of([], MaxHeartRate.Estimate(22, null), heartRateDays: 0));

    [Fact]
    public void Sessions_mostly_without_heart_rate_are_unknown_cardio_rather_than_low()
    {
        var start = new DateTimeOffset(2026, 11, 5, 18, 0, 0, TimeSpan.FromHours(-5));
        var unrecorded = new CardioSession(
            new CardioSessionSource.RingWorkout(Guid.NewGuid(), "running", WorkoutCategory.Endurance),
            start, start.AddHours(1), 8000, new SessionHeartRate.NotRecorded());

        Assert.Equal(
            new CardioMeasure.Unknown(CardioUnknownReason.SessionsUnrecorded),
            CardioMeasures.Of([unrecorded], MaxHeartRate.Estimate(22, null), heartRateDays: 6));
    }

    [Fact]
    public void Zone_two_and_zones_four_and_five_are_summed_across_the_sessions()
    {
        var start = new DateTimeOffset(2026, 11, 5, 18, 0, 0, TimeSpan.FromHours(-5));
        CardioSession Run(ZoneMinutes zones) => new(
            new CardioSessionSource.RingWorkout(Guid.NewGuid(), "running", WorkoutCategory.Endurance),
            start, start.AddHours(1), null, new SessionHeartRate.Recorded(140, 180, 1, zones));

        var measured = Assert.IsType<CardioMeasure.Measured>(CardioMeasures.Of(
            [Run(new ZoneMinutes(0, 5, 40, 10, 4, 1)), Run(new ZoneMinutes(0, 0, 20, 0, 0, 0))],
            MaxHeartRate.Estimate(22, null),
            heartRateDays: 7));

        Assert.Equal((60d, 5d, 2), (measured.Zone2Minutes, measured.HighIntensityMinutes, measured.Sessions));
    }
}
