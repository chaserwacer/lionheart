using lionheart.Model.Cardio;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Training.Sets;

namespace lionheart.Tests.Cardio;

/// <summary>
/// Which high heart rate earns intensity credit: work bouts held at zone 4 or above, in an
/// endurance session, adding up to a session's worth.
/// </summary>
public class QualityWorkTests
{
    // A maximum of 200 puts zone 4 at 160 bpm and above.
    private const int Maximum = 200;
    private static readonly DateTimeOffset Start = new(2026, 10, 14, 18, 0, 0, TimeSpan.FromHours(-4));

    [Fact]
    public void Three_minutes_held_at_zone_four_is_one_bout()
    {
        var bout = Assert.Single(Bouts(Every(5, (170, 180), (130, 60))));

        Assert.Equal(3, bout.Minutes, precision: 3);
        Assert.Equal(170, bout.AverageBpm);
    }

    [Fact]
    public void A_surge_shorter_than_two_minutes_is_not_a_bout() =>
        Assert.Empty(Bouts(Every(5, (130, 60), (175, 90), (130, 60))));

    [Fact]
    public void A_dip_of_half_a_minute_or_less_does_not_end_a_bout_and_is_not_counted_in_it()
    {
        var bout = Assert.Single(Bouts(Every(5, (170, 120), (150, 20), (170, 120), (130, 60))));

        Assert.Equal(4, bout.Minutes, precision: 3);
    }

    [Fact]
    public void A_recovery_of_a_minute_separates_two_bouts() =>
        Assert.Equal([2d, 2d], Bouts(Every(5, (170, 120), (140, 60), (170, 120), (130, 60))).Select(bout => Math.Round(bout.Minutes, 3)));

    [Fact]
    public void A_sparse_background_series_cannot_be_read_as_intervals() =>
        // Half an hour at zone 4, one reading every five minutes.
        Assert.Empty(Bouts(Every(300, (175, 1800))));

    [Fact]
    public void Quality_minutes_are_the_bouts_of_an_endurance_session_holding_ten_or_more()
    {
        var intervals = Recorded(new WorkBout(Start, 6, 170), new WorkBout(Start.AddMinutes(10), 6, 172));

        Assert.Equal(12, QualityWork.MinutesOf(Run, intervals));
    }

    [Fact]
    public void A_session_holding_less_than_ten_minutes_of_bouts_earns_none() =>
        Assert.Equal(0, QualityWork.MinutesOf(Run, Recorded(new WorkBout(Start, 8, 170))));

    [Theory]
    [MemberData(nameof(NotEndurance))]
    public void Bouts_in_a_game_a_walk_or_a_hike_earn_no_quality_minutes(CardioSessionSource source) =>
        Assert.Equal(0, QualityWork.MinutesOf(source, Recorded(new WorkBout(Start, 20, 170))));

    public static TheoryData<CardioSessionSource> NotEndurance => new()
    {
        new CardioSessionSource.RingWorkout(Guid.NewGuid(), "basketball", WorkoutCategory.Sport, RingEntry.Entered),
        new CardioSessionSource.RingWorkout(Guid.NewGuid(), "hiking", WorkoutCategory.Endurance, RingEntry.Entered),
        new CardioSessionSource.LoggedSession(Guid.NewGuid(), false, true, EnduranceActivity.Hike),
        new CardioSessionSource.LoggedSession(Guid.NewGuid(), false, true, EnduranceActivity.Walk),
        new CardioSessionSource.RingWorkout(Guid.NewGuid(), "other", WorkoutCategory.Unspecified, RingEntry.Entered),
    };

    [Theory]
    [MemberData(nameof(Endurance))]
    public void Runs_rides_and_machines_are_endurance(CardioSessionSource source) =>
        Assert.Equal(CardioModality.Endurance, QualityWork.ModalityOf(source));

    public static TheoryData<CardioSessionSource> Endurance => new()
    {
        new CardioSessionSource.LoggedSession(Guid.NewGuid(), true, true),
        new CardioSessionSource.LoggedSession(Guid.NewGuid(), false, true, EnduranceActivity.IndoorRide),
        new CardioSessionSource.LoggedSession(Guid.NewGuid(), false, false),
        new CardioSessionSource.RingWorkout(Guid.NewGuid(), "rowing", WorkoutCategory.Endurance, RingEntry.Detected),
    };

    private static readonly CardioSessionSource Run = new CardioSessionSource.LoggedSession(Guid.NewGuid(), true, true);

    private static SessionHeartRate Recorded(params WorkBout[] bouts) =>
        new SessionHeartRate.Recorded(150, 180, 1, ZoneMinutes.None, HeartRateSource.AppleHealth) { Bouts = bouts };

    private static IReadOnlyList<WorkBout> Bouts(IReadOnlyList<HeartRateSample> samples) =>
        WorkBouts.Of(samples, samples[^1].At.AddSeconds(5), Maximum);

    /// <summary>A sample every <paramref name="seconds"/> from the start, at each rate for its seconds.</summary>
    private static IReadOnlyList<HeartRateSample> Every(int seconds, params (int Bpm, int Seconds)[] stretches) =>
    [
        .. stretches
            .SelectMany(stretch => Enumerable.Repeat(stretch.Bpm, stretch.Seconds / seconds))
            .Select((bpm, index) => new HeartRateSample(Start.AddSeconds(seconds * index), bpm, "workout"))
    ];
}
