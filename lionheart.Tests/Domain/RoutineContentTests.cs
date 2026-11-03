using lionheart.Functional;
using lionheart.Model.Training.Routines;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;

namespace lionheart.Tests.Domain;

/// <summary>
/// What a routine may say, and how a completed session's sets read as one.
/// </summary>
public class RoutineContentTests
{
    private static readonly Guid Pulldown = Guid.NewGuid();
    private static readonly Guid Press = Guid.NewGuid();

    [Fact]
    public void A_routine_keeps_its_movement_order_and_a_trimmed_name()
    {
        var content = ResultAssert.Succeeded(RoutineContent.Of(
            "  Upper day ", "pin 7", [new RoutineMovement(Press, 2), new RoutineMovement(Pulldown, 3)]));

        Assert.Equal("Upper day", content.Name);
        Assert.Equal([new RoutineMovement(Press, 2), new RoutineMovement(Pulldown, 3)], content.Movements);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_routine_with_no_name_is_refused_on_its_name(string name) =>
        Assert.Equal(
            new InvalidField("Name", "A routine needs a name."),
            Single(RoutineContent.Of(name, string.Empty, [new RoutineMovement(Press, 2)])));

    [Fact]
    public void A_routine_with_no_movement_is_refused_on_its_movements() =>
        Assert.Equal("Movements", Single(RoutineContent.Of("Upper", string.Empty, [])).Field);

    [Fact]
    public void A_movement_named_twice_is_refused() =>
        Assert.Equal(
            new InvalidField("Movements", "Each movement appears in a routine once."),
            Single(RoutineContent.Of("Upper", string.Empty, [new RoutineMovement(Press, 2), new RoutineMovement(Press, 3)])));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(21)]
    public void A_working_set_count_outside_one_to_twenty_is_refused_naming_the_movement(int workingSets) =>
        Assert.Equal(
            "Movements[1].WorkingSets",
            Single(RoutineContent.Of(
                "Upper", string.Empty, [new RoutineMovement(Press, 2), new RoutineMovement(Pulldown, workingSets)])).Field);

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public void One_and_twenty_working_sets_are_both_a_routine(int workingSets) =>
        ResultAssert.Succeeded(RoutineContent.Of("Upper", string.Empty, [new RoutineMovement(Press, workingSets)]));

    [Fact]
    public void A_sessions_movements_read_in_first_appearance_order_counting_every_set_but_warm_ups_and_drops()
    {
        IReadOnlyList<SetEntry> sets =
        [
            Lift(Press, SetKind.WarmUp),
            Lift(Press, SetKind.Working),
            Lift(Pulldown, SetKind.Working),
            Lift(Press, SetKind.Failure),
            Lift(Pulldown, SetKind.Drop),
            Some.DistanceTimeSet with { MovementDataId = Press },
        ];

        Assert.Equal(
            [new RoutineMovement(Press, 3), new RoutineMovement(Pulldown, 1)],
            RoutineMovement.TrainedIn(sets));
    }

    [Fact]
    public void A_movement_trained_only_in_warm_ups_is_left_out() =>
        Assert.Equal(
            [new RoutineMovement(Press, 1)],
            RoutineMovement.TrainedIn([Lift(Pulldown, SetKind.WarmUp), Lift(Press, SetKind.Working)]));

    [Fact]
    public void Sets_that_name_no_movement_add_none() =>
        Assert.Empty(RoutineMovement.TrainedIn(
            [Some.TrailRunSet, Some.FreeformSet, Lift(Guid.Empty, SetKind.Working)]));

    private static InvalidField Single(Result<RoutineContent, Problem> refused) =>
        Assert.Single(Assert.IsType<Problem.Invalid>(ResultAssert.Failed(refused)).Fields);

    private static LiftSet Lift(Guid movement, SetKind kind) =>
        new(movement, 5, 100, WeightUnit.Pounds, Rpe.Unrated, kind) { Source = new Formed(), Facets = [] };
}
