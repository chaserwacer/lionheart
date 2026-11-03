using lionheart.Model.Analysis;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;

namespace lionheart.Tests.Domain;

/// <summary>
/// The "previous" column: for each movement, its sets from the most recent completed session
/// that holds it.
/// </summary>
public class PreviousPerformanceTests
{
    private static readonly Guid Press = Guid.NewGuid();
    private static readonly Guid Pulldown = Guid.NewGuid();
    private static readonly DateTimeOffset Monday = new(2026, 11, 2, 17, 0, 0, TimeSpan.FromHours(-5));

    [Fact]
    public void Each_movement_comes_from_the_latest_session_holding_it_however_the_sessions_arrive()
    {
        var older = Session(Monday, Lift(Press, 200), Lift(Pulldown, 150));
        var newer = Session(Monday.AddDays(2), Lift(Press, 205));

        var performances = PreviousPerformances.Of([newer, older]).ToDictionary(p => p.MovementDataId);

        Assert.Equal(newer.TrainingSessionId, performances[Press].TrainingSessionId);
        Assert.Equal(older.TrainingSessionId, performances[Pulldown].TrainingSessionId);
        Assert.Equal(
            PreviousPerformances.Of([older, newer]).Select(p => (p.MovementDataId, p.TrainingSessionId)).Order(),
            performances.Values.Select(p => (p.MovementDataId, p.TrainingSessionId)).Order());
    }

    [Fact]
    public void A_performance_holds_that_movements_sets_in_the_order_performed_warm_ups_included()
    {
        var session = Session(
            Monday,
            Lift(Press, 135, SetKind.WarmUp),
            Lift(Pulldown, 150),
            Lift(Press, 205),
            Lift(Press, 185, SetKind.Drop));

        var press = Assert.Single(PreviousPerformances.Of([session]), p => p.MovementDataId == Press);

        Assert.Equal(
            [(135.0, SetKind.WarmUp), (205, SetKind.Working), (185, SetKind.Drop)],
            press.Sets.Cast<LiftSet>().Select(set => (set.Weight, set.Kind)));
    }

    [Fact]
    public void A_performance_says_when_its_session_started_and_on_which_day()
    {
        var late = new DateTimeOffset(2026, 11, 2, 23, 30, 0, TimeSpan.FromHours(-5));

        var performance = Assert.Single(PreviousPerformances.Of([Session(late, Lift(Press, 205))]));

        Assert.Equal(late, performance.StartedAt);
        Assert.Equal(new DateOnly(2026, 11, 2), performance.Date);
    }

    [Fact]
    public void Sets_with_no_movement_never_appear() =>
        Assert.Empty(PreviousPerformances.Of([Session(Monday, Some.TrailRunSet, Some.FreeformSet)]));

    private static CompletedTrainingSession Session(DateTimeOffset start, params SetEntry[] sets) =>
        Some.CompletedSession(Guid.NewGuid()) with
        {
            Sets = sets,
            Period = new Interval(start, start.AddHours(1)),
        };

    private static LiftSet Lift(Guid movement, double weight, SetKind kind = SetKind.Working) =>
        new(movement, 5, weight, WeightUnit.Pounds, Rpe.Unrated, kind) { Source = new Formed(), Facets = [] };
}
