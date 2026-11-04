using lionheart.Functional;
using lionheart.Model.Training.Patterns;
using lionheart.Tests.Support;

namespace lionheart.Tests.Balance;

/// <summary>
/// What a classification may say: at least one primary muscle, no muscle both primary and
/// secondary, and a family that follows the pattern or, for accessory work, the first primary muscle.
/// </summary>
public class MovementClassificationTests
{
    [Fact]
    public void A_classification_with_no_primary_muscle_is_refused_naming_primary()
    {
        var refusal = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(
            MovementClassification.Of(MovementPattern.Isolation, [], [MuscleGroup.Forearms])));

        Assert.Equal("Primary", Assert.Single(refusal.Fields).Field);
    }

    [Fact]
    public void A_muscle_both_primary_and_secondary_is_refused_naming_secondary()
    {
        var refusal = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(
            MovementClassification.Of(MovementPattern.HorizontalPush, [MuscleGroup.Chest], [MuscleGroup.Chest])));

        Assert.Equal("Secondary", Assert.Single(refusal.Fields).Field);
    }

    [Fact]
    public void Every_problem_is_reported_at_once()
    {
        var refusal = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(
            MovementClassification.Of((MovementPattern)42, [], [(MuscleGroup)42])));

        Assert.Equal(
            ["Pattern", "Primary", "Secondary"],
            refusal.Fields.Select(field => field.Field).Order());
    }

    [Fact]
    public void Repeated_muscles_are_kept_once_in_the_order_first_given()
    {
        var trains = ResultAssert.Succeeded(MovementClassification.Of(
            MovementPattern.Hinge, [MuscleGroup.Glutes, MuscleGroup.Hamstrings, MuscleGroup.Glutes], []));

        Assert.Equal([MuscleGroup.Glutes, MuscleGroup.Hamstrings], trains.Primary);
    }

    [Theory]
    [InlineData(MovementPattern.VerticalPush, MuscleGroup.FrontDelts, PatternFamily.Push)]
    [InlineData(MovementPattern.VerticalPull, MuscleGroup.Lats, PatternFamily.Pull)]
    [InlineData(MovementPattern.Hinge, MuscleGroup.LowerBack, PatternFamily.Legs)]
    [InlineData(MovementPattern.Squat, MuscleGroup.Quads, PatternFamily.Legs)]
    [InlineData(MovementPattern.SingleLeg, MuscleGroup.Glutes, PatternFamily.Legs)]
    public void A_compound_pattern_names_its_own_family_whatever_the_muscles(
        MovementPattern pattern, MuscleGroup primary, PatternFamily family) =>
        Assert.Equal(family, ResultAssert.Succeeded(MovementClassification.Of(pattern, [primary], [])).Family);

    [Theory]
    [InlineData(MuscleGroup.Biceps, PatternFamily.Pull)]
    [InlineData(MuscleGroup.SideDelts, PatternFamily.Push)]
    [InlineData(MuscleGroup.Calves, PatternFamily.Legs)]
    [InlineData(MuscleGroup.Abs, PatternFamily.Core)]
    public void Accessory_work_takes_the_family_of_its_first_primary_muscle(MuscleGroup first, PatternFamily family) =>
        Assert.Equal(family, ResultAssert.Succeeded(
            MovementClassification.Of(MovementPattern.Isolation, [first, MuscleGroup.Chest], [])).Family);

    [Fact]
    public void A_set_pays_a_primary_muscle_whole_a_secondary_half_and_others_nothing()
    {
        var bench = ResultAssert.Succeeded(MovementClassification.Of(
            MovementPattern.HorizontalPush, [MuscleGroup.Chest], [MuscleGroup.Triceps]));

        Assert.Equal((1, 0.5, 0), (bench.ShareOf(MuscleGroup.Chest), bench.ShareOf(MuscleGroup.Triceps), bench.ShareOf(MuscleGroup.Lats)));
    }

    [Fact]
    public void A_family_priority_is_for_push_pull_or_legs_never_core()
    {
        Assert.Equal(
            "Priority",
            Assert.Single(Assert.IsType<Problem.Invalid>(ResultAssert.Failed(
                Model.Training.Focus.FocusRole.Priority.For(PatternFamily.Core))).Fields).Field);
        Assert.All(MovementPatterns.Judged, family => ResultAssert.Succeeded(Model.Training.Focus.FocusRole.Priority.For(family)));
    }
}
