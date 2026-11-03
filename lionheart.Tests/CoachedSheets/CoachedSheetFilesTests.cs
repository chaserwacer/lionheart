using lionheart.Functional;
using lionheart.Services.CoachedSheets;
using lionheart.Services.Import;
using lionheart.Services.Strong;
using lionheart.Tests.Support;

namespace lionheart.Tests.CoachedSheets;

/// <summary>
/// The two shipped files the coached-programme import reads — the movement map and the sheet
/// decisions — as shipped and as edited, and the notes the decisions shape.
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
    public void Blakes_sheets_pull_sumo_and_the_Jamal_block_pulls_conventional()
    {
        // The sheets never state the stance; the athlete does. Blake's 463 x 3 and 474 x 3 are past
        // his 463 conventional single, and he pulled conventional while running Jamal.
        var decisions = ResultAssert.Succeeded(SheetDecisions.Shipped());
        var movements = ResultAssert.Succeeded(Movements.Cover(["deadlift-barbell", "sumo-deadlift-barbell"]));

        Assert.All(
            new[] { "disabled", "mk-2", "dont-take-3-months-for-this-one", "chase-vs-his-opps" },
            sheet => Assert.Equal("sumo-deadlift-barbell", decisions.Programs[sheet].Reading("deadlift-barbell")));
        Assert.Equal("deadlift-barbell", decisions.Programs["your-program-rpe"].Reading("deadlift-barbell"));
        Assert.Equal("Sumo Deadlift", movements["sumo-deadlift-barbell"].Movement.Base);
        Assert.Equal("Deadlift", movements["deadlift-barbell"].Movement.Base);
    }

    // ---- The sheet decisions ----

    [Fact]
    public void The_shipped_decisions_read_and_give_every_sheet_a_programme()
    {
        var decisions = ResultAssert.Succeeded(SheetDecisions.Shipped());

        Assert.Equal(
            ["chase-vs-his-opps", "disabled", "dont-take-3-months-for-this-one", "mk-2", "your-program-rpe"],
            decisions.Programs.Keys.Order(StringComparer.Ordinal));
        Assert.All(decisions.NotPerformed, row => Assert.NotEmpty(row.Reason));
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
    [InlineData("prescribed RPE 45147.0 | Pinky on ring", "Pinky on ring")]
    [InlineData("prescribed RPE 265.0", "")]
    [InlineData("prescribed RPE -%", "")]
    [InlineData("prescribed RPE 8.5-9 | PLEASE CHECK THE FAQ PAGE. IT EXPLAINS THE BACKDOWN SETS.", "prescribed RPE 8.5-9")]
    public void A_coach_note_keeps_his_words_and_drops_template_text_and_misfiled_ratings(string note, string kept)
    {
        Assert.Equal(kept, SheetSessions.CoachNote(note, new HashSet<string>(["PLEASE CHECK THE FAQ PAGE"])));
    }
}
