using lionheart.Functional;
using lionheart.Services.Strong;
using lionheart.Tests.Support;

namespace lionheart.Tests.Strong;

/// <summary>
/// The athlete-editable map from Strong's exercise names to movements, as shipped and as edited.
/// </summary>
public class StrongMovementMapTests
{
    private static StrongMovementMap Shipped => ResultAssert.Succeeded(StrongMovementMap.Shipped());

    // ---- The shipped map ----

    [Fact]
    public void The_shipped_map_reads_and_covers_every_exercise_in_the_real_workouts()
    {
        var exercises = ResultAssert.Succeeded(StrongExportReader.Read(StrongExports.FourWorkouts, StrongExports.NewYork))
            .SelectMany(workout => workout.Sets)
            .Select(set => set.Exercise);

        ResultAssert.Succeeded(Shipped.Cover(exercises));
    }

    [Fact]
    public void Movements_the_athlete_already_has_are_named_the_way_his_catalogue_names_them()
    {
        // His catalogue spells it "Dumbell" and calls the incline machine its own equipment. A map
        // written in Strong's spelling would grow a second dumbbell beside the first.
        var covered = ResultAssert.Succeeded(Shipped.Cover(["Bicep Curl (Dumbbell)", "Incline Chest Press (Machine)"]));

        Assert.Equal(new MovementName("Bicep Curl", "Dumbell", ""), covered["Bicep Curl (Dumbbell)"].Movement);
        Assert.Equal(
            new MovementName("Bench Press", "Incline Machine Press", "incline"),
            covered["Incline Chest Press (Machine)"].Movement);
    }

    [Theory]
    [InlineData("Incline Curl (Dumbbell)", "Incline Seated Bicep Curl")]
    [InlineData("Full ROM Single Arm Row Cable", "cable single arm row toes to head")]
    public void Two_names_for_one_exercise_fold_into_one_movement(string one, string other)
    {
        var covered = ResultAssert.Succeeded(Shipped.Cover([one, other]));

        Assert.Equal(covered[one].Movement, covered[other].Movement);
    }

    [Fact]
    public void A_name_Strong_typed_with_odd_capitals_and_spaces_finds_its_entry()
    {
        var covered = ResultAssert.Succeeded(Shipped.Cover(["SHOULDER PRESS DUMBELL  "]));

        Assert.Equal(new MovementName("Shoulder Press", "Dumbell", ""), Assert.Single(covered.Values).Movement);
    }

    [Fact]
    public void An_unsure_mapping_says_why()
    {
        var covered = ResultAssert.Succeeded(Shipped.Cover(["Tempo Deadlift", "Squat (Barbell)"]));

        Assert.True(covered["Tempo Deadlift"].IsUnsure);
        Assert.False(covered["Squat (Barbell)"].IsUnsure);
    }

    // ---- Refusals ----

    [Fact]
    public void Every_exercise_the_map_does_not_cover_is_named()
    {
        var problem = ResultAssert.Failed(Shipped.Cover(["Squat (Barbell)", "Zercher Squat", "Jefferson Curl"]));

        Assert.Equal(
            ["Jefferson Curl", "Zercher Squat"],
            Assert.IsType<Problem.Invalid>(problem).Fields.Select(field => field.Field));
    }

    [Fact]
    public void Two_entries_for_one_name_are_refused_rather_than_one_silently_winning()
    {
        const string file = """
            [
              { "strong": "Squat (Barbell)", "base": "Squat", "equipment": "Barbell" },
              { "strong": "squat (barbell) ", "base": "Front Squat", "equipment": "Barbell" }
            ]
            """;

        var problem = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(StrongMovementMap.Parse(file)));
        Assert.Contains("2 entries", problem.Description);
    }

    [Fact]
    public void An_entry_without_a_base_or_equipment_is_refused()
    {
        const string file = """
            // comments are allowed, so the athlete can annotate the file
            [ { "strong": "Squat (Barbell)", "base": "Squat" }, ]
            """;

        var problem = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(StrongMovementMap.Parse(file)));
        Assert.Equal("Squat (Barbell)", Assert.Single(problem.Fields).Field);
    }

    [Fact]
    public void A_file_that_is_not_JSON_is_refused_by_name()
    {
        var problem = ResultAssert.Failed(StrongMovementMap.Parse("[ { \"strong\": "));

        Assert.Contains("strong-movements.json cannot be read", problem.Description);
    }
}
