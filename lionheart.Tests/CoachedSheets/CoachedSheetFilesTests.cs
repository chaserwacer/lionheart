using lionheart.Functional;
using lionheart.Services.CoachedSheets;
using lionheart.Services.Import;
using lionheart.Services.Strong;
using lionheart.Tests.Support;

namespace lionheart.Tests.CoachedSheets;

/// <summary>
/// The two files the coached-programme import reads — the shipped movement map and the sheet
/// decisions the command line names — and the notes the decisions shape.
/// </summary>
public class CoachedSheetFilesTests
{
    private static MovementMap Movements => ResultAssert.Succeeded(SheetMovementMap.Shipped());

    // ---- The movement map ----

    [Theory]
    [InlineData("3-count-pause-bench", "3Ct Bench Press")]
    [InlineData("tempo-deadlift", "Tempo Deadlift")]
    [InlineData("ffe-split-squat", "Ffe Split Squat")]
    [InlineData("squat-barbell", "Squat (Barbell)")]
    [InlineData("bench-press-barbell", "Bench Press (Barbell)")]
    public void A_lift_the_Strong_history_already_holds_maps_to_the_same_movement(string slug, string strongName)
    {
        var strong = ResultAssert.Succeeded(ResultAssert.Succeeded(StrongMovementMap.Shipped()).Cover([strongName]));
        var sheets = ResultAssert.Succeeded(Movements.Cover([slug]));

        Assert.Equal(strong[strongName].Movement, sheets[slug].Movement);
    }

    [Theory]
    [InlineData("tempo-squat")]
    [InlineData("paused-squat")]
    [InlineData("paused-ssb-squat")]
    [InlineData("ascending-highbar-squat")]
    public void A_squat_variation_stays_a_movement_of_its_own(string slug)
    {
        var movement = ResultAssert.Succeeded(Movements.Cover([slug, "squat-barbell"]));

        Assert.NotEqual(movement["squat-barbell"].Movement, movement[slug].Movement);
        Assert.True(movement[slug].Movement.HasModifier);
    }

    [Fact]
    public void The_sumo_slugs_a_programme_can_read_its_deadlifts_as_map_to_sumo_movements()
    {
        var movements = ResultAssert.Succeeded(Movements.Cover(["deadlift-barbell", "sumo-deadlift-barbell", "paused-sumo-deadlift"]));

        Assert.Equal("Sumo Deadlift", movements["sumo-deadlift-barbell"].Movement.Base);
        Assert.Equal("Sumo Deadlift", movements["paused-sumo-deadlift"].Movement.Base);
        Assert.Equal("Deadlift", movements["deadlift-barbell"].Movement.Base);
    }

    // ---- The sheet decisions ----

    [Fact]
    public void A_decisions_file_is_read_from_the_path_it_is_named_by()
    {
        var path = Path.Combine(Path.GetTempPath(), $"coached-sheets-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            // comments are allowed
            { "programs": [ { "sheet": "mk-2", "title": "Strength block — MK.2", "readAs": { "deadlift-barbell": "sumo-deadlift-barbell" } } ] }
            """);

        try
        {
            var decisions = ResultAssert.Succeeded(SheetDecisions.Read(path));

            Assert.Equal("sumo-deadlift-barbell", decisions.Programs["mk-2"].Reading("deadlift-barbell"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void The_example_decisions_file_reads()
    {
        var example = Path.Combine(AppContext.BaseDirectory, "CoachedSheets", "coached-sheets.example.json");

        var decisions = ResultAssert.Succeeded(SheetDecisions.Read(example));

        Assert.Equal("sumo-deadlift-barbell", decisions.Programs["block-two"].Reading("deadlift-barbell"));
        Assert.Equal([NotPerformedKind.Computed, NotPerformedKind.Implausible], decisions.NotPerformed.Select(row => row.Kind));
    }

    [Fact]
    public void A_decisions_path_with_no_file_is_refused_by_name()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.json");

        var problem = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(SheetDecisions.Read(missing)));

        Assert.Contains(missing, Assert.Single(problem.Fields).Message);
    }

    [Fact]
    public void A_not_performed_row_of_an_unknown_kind_is_refused()
    {
        const string file = """
            // comments are allowed
            { "programs": [], "notPerformed": [
              { "source": "sheet:mk-2#w1", "date": "2023-12-18", "exercise": "Deadlift", "weight": 407, "reps": 3, "sets": 1, "kind": "skipped", "reason": "x" },
            ] }
            """;

        var problem = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(SheetDecisions.Parse(file)));

        Assert.Contains("'skipped' that is not one of computed, placeholder, contradicted, implausible", Assert.Single(problem.Fields).Message);
    }

    [Fact]
    public void A_decisions_file_that_is_not_JSON_is_refused_by_name()
    {
        var problem = ResultAssert.Failed(SheetDecisions.Parse("{ \"programs\": ["));

        Assert.Contains("coached-sheets.json cannot be read", problem.Description);
    }

    // ---- Notes ----

    [Theory]
    [InlineData("prescribed RPE 6.5", "prescribed RPE 6.5")]
    [InlineData("prescribed RPE 45147.0 | Grip a finger wider", "Grip a finger wider")]
    [InlineData("prescribed RPE 265.0", "")]
    [InlineData("prescribed RPE -%", "")]
    [InlineData("prescribed RPE 8.5-9 | PLEASE CHECK THE FAQ PAGE. IT EXPLAINS THE BACKDOWN SETS.", "prescribed RPE 8.5-9")]
    public void A_coach_note_keeps_his_words_and_drops_template_text_and_misfiled_ratings(string note, string kept)
    {
        Assert.Equal(kept, SheetSessions.CoachNote(note, new HashSet<string>(["PLEASE CHECK THE FAQ PAGE"])));
    }
}
