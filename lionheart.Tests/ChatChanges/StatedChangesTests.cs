using lionheart.Functional;
using lionheart.Model.Chat.Changes;
using lionheart.Model.InjuryManagement;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Tests.Support;

namespace lionheart.Tests.ChatChanges;

/// <summary>
/// What the model sends, read into the page's request or refused naming the field it got wrong,
/// before anything about the athlete is looked up.
/// </summary>
public sealed class StatedChangesTests
{
    private static readonly DateOnly Day = new(2026, 11, 11);
    private static readonly Guid Squat = Guid.NewGuid();

    private static string FieldOf(Problem problem) => Assert.Single(Assert.IsType<Problem.Invalid>(problem).Fields).Field;

    [Fact]
    public void A_lift_is_prescribed_once_per_set_in_pounds_and_freeform_lines_follow_it()
    {
        var plan = ResultAssert.Succeeded(StatedChanges.Plan(new PlannedSessionArguments
        {
            Day = Day,
            Lifts = [new PlannedLift { MovementDataId = Squat, Sets = 3, Reps = 5, WeightLb = 225, Rpe = 8 }],
            Freeform = ["  Easy run, 3 miles  "],
            Notes = " before work ",
        }));

        Assert.Equal(
            [
                .. Enumerable.Repeat<SetPrescriptionCommand>(new LiftPrescriptionCommand(Squat, 5, 225, WeightUnit.Pounds, 8), 3),
                new FreeformPrescriptionCommand("Easy run, 3 miles"),
            ],
            plan.Prescriptions);
        Assert.Equal((Day, "before work"), (plan.Day, plan.Notes));
    }

    [Fact]
    public void A_plan_that_says_nothing_about_the_session_is_refused()
    {
        Assert.Equal("Lifts", FieldOf(ResultAssert.Failed(StatedChanges.Plan(new PlannedSessionArguments { Day = Day, Notes = "something" }))));
    }

    [Fact]
    public void A_plan_with_no_day_is_refused_naming_the_day()
    {
        Assert.Equal("Day", FieldOf(ResultAssert.Failed(StatedChanges.Plan(new PlannedSessionArguments { Freeform = ["Run"] }))));
    }

    [Theory]
    [InlineData(0, 5, 100, "Sets")]
    [InlineData(11, 5, 100, "Sets")]
    [InlineData(3, 0, 100, "Reps")]
    [InlineData(3, 51, 100, "Reps")]
    [InlineData(3, 5, -1, "WeightLb")]
    public void A_lift_out_of_range_is_refused_naming_the_field(int sets, int reps, double weight, string field)
    {
        var problem = ResultAssert.Failed(StatedChanges.Plan(new PlannedSessionArguments
        {
            Day = Day,
            Lifts = [new PlannedLift { MovementDataId = Squat, Sets = sets, Reps = reps, WeightLb = weight }],
        }));

        Assert.Equal(field, FieldOf(problem));
    }

    [Fact]
    public void A_lift_with_no_movement_is_refused()
    {
        var problem = ResultAssert.Failed(StatedChanges.Plan(new PlannedSessionArguments
        {
            Day = Day,
            Lifts = [new PlannedLift { Sets = 3, Reps = 5, WeightLb = 100 }],
        }));

        Assert.Equal("MovementDataId", FieldOf(problem));
    }

    [Theory]
    [InlineData(null, NonTrainingRepeat.Once)]
    [InlineData("once", NonTrainingRepeat.Once)]
    [InlineData("Weekly", NonTrainingRepeat.Weekly)]
    public void A_repeat_is_read_from_its_word(string? word, NonTrainingRepeat repeat)
    {
        var request = ResultAssert.Succeeded(StatedChanges.NonTrainingDay(new NonTrainingDayArguments { Day = Day, Repeat = word }));

        Assert.Equal(repeat, request.Repeat);
    }

    [Fact]
    public void A_repeat_in_other_words_is_refused()
    {
        Assert.Equal("Repeat", FieldOf(ResultAssert.Failed(StatedChanges.NonTrainingDay(new NonTrainingDayArguments { Day = Day, Repeat = "monthly" }))));
    }

    [Fact]
    public void A_last_day_on_a_one_off_day_is_refused_as_the_page_refuses_it()
    {
        Assert.Equal("Until", FieldOf(ResultAssert.Failed(StatedChanges.NonTrainingDay(
            new NonTrainingDayArguments { Day = Day, Until = Day.AddDays(7) }))));
    }

    [Fact]
    public void A_priority_starts_today_when_no_first_day_is_given()
    {
        var stated = ResultAssert.Succeeded(StatedChanges.PriorityLift(
            new PriorityLiftArguments { Family = "Legs", MovementDataId = Squat, Ends = Day.AddDays(42), SessionsPerWeek = 2 }, today: Day));

        Assert.Equal((PatternFamily.Legs, Day, PatternFamily.Legs), (stated.Family, stated.Request.Starts, stated.Request.Priority));
        Assert.Null(stated.Request.Target);
    }

    [Theory]
    [InlineData("core")]
    [InlineData("arms")]
    [InlineData(null)]
    public void A_priority_leads_push_pull_or_legs_only(string? family)
    {
        Assert.Equal("Family", FieldOf(ResultAssert.Failed(StatedChanges.PriorityLift(
            new PriorityLiftArguments { Family = family, MovementDataId = Squat, Ends = Day, SessionsPerWeek = 2 }, Day))));
    }

    [Fact]
    public void A_target_needs_its_weight_and_its_reps()
    {
        Assert.Equal("TargetReps", FieldOf(ResultAssert.Failed(StatedChanges.PriorityLift(
            new PriorityLiftArguments { Family = "push", MovementDataId = Squat, Ends = Day, SessionsPerWeek = 2, TargetWeightLb = 100 }, Day))));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Pain_is_zero_to_ten(int pain)
    {
        Assert.Equal("PainLevel", FieldOf(ResultAssert.Failed(StatedChanges.InjuryCheckIn(
            new InjuryCheckInArguments { InjuryId = Guid.NewGuid(), PainLevel = pain }))));
    }

    [Fact]
    public void A_treatment_is_logged_as_one()
    {
        var request = ResultAssert.Succeeded(StatedChanges.InjuryCheckIn(
            new InjuryCheckInArguments { InjuryId = Guid.NewGuid(), PainLevel = 2, Treatment = true }));

        Assert.Equal(InjuryEventType.treatment, request.InjuryType);
    }
}
