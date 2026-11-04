using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Model.Health;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Services.Cardio;

namespace lionheart.Tests.Cardio;

/// <summary>
/// The Apple Watch's VO2max beside Oura's in the cardio report: its own series and its own
/// comparison, so a change of device never reads as a change of fitness.
/// </summary>
public class AppleWatchVo2MaxTests
{
    private static readonly Guid Athlete = Guid.NewGuid();
    private static readonly DayRange Spring = new(new DateOnly(2022, 3, 1), new DateOnly(2022, 5, 31));

    [Fact]
    public void The_watchs_estimates_in_the_range_are_their_own_series_and_never_join_Ouras()
    {
        var report = Build(
            watch: [new DayValue(new DateOnly(2022, 4, 2), 51.3), new DayValue(new DateOnly(2021, 4, 2), 49)],
            oura: [OuraEstimate(new DateOnly(2022, 4, 3), 47)]);

        Assert.Equal([new DayValue(new DateOnly(2022, 4, 2), 51.3)], report.AppleWatchVo2Max);
        Assert.Equal([new DayValue(new DateOnly(2022, 4, 3), 47)], report.Vo2Max);
    }

    [Fact]
    public void The_aerobic_base_compares_the_watchs_estimates_window_against_window_apart_from_Ouras()
    {
        var report = Build(
            watch: [new DayValue(new DateOnly(2022, 5, 20), 52), new DayValue(new DateOnly(2022, 4, 1), 50)],
            oura: []);

        Assert.Equal(new Comparison(52, 50), report.AerobicBase.AppleWatchVo2Max);
        Assert.Equal(new Comparison(null, null), report.AerobicBase.Vo2Max);
    }

    private static CardioReport Build(IReadOnlyList<DayValue> watch, IReadOnlyList<OuraVo2Max> oura) =>
        CardioAnalysis.Build(new CardioInputs(
            Spring, 30, CardioAnalysis.Gather([], []), [], new Dictionary<Guid, RecordedHeartRate>(), oura, [], [])
        {
            AppleWatchVo2Max = watch,
        });

    private static OuraVo2Max OuraEstimate(DateOnly day, int value) =>
        new(Guid.NewGuid(), Athlete, day, new Vo2MaxReading(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), value));
}
