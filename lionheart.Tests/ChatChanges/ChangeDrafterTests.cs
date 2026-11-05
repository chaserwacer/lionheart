using lionheart.Functional;
using lionheart.Model.Chat.Changes;
using lionheart.Model.InjuryManagement;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.ChatChanges;

/// <summary>
/// What the propose tools hand the athlete: a card in his words and units, checked against his data
/// so it will go through — and nothing written, whatever the model asked for.
/// </summary>
public sealed class ChangeDrafterTests : IDisposable
{
    private static readonly DateOnly NextWednesday = ChangeDesk.Today.AddDays(7);

    private readonly ChangeDesk _desk = new();

    public void Dispose() => _desk.Dispose();

    private Guid Owner => _desk.Athletes.OwnerId;

    private static PlannedSessionArguments ARun(DateOnly day) => new()
    {
        Day = day,
        Freeform = ["Easy run, 4 miles, conversational"],
        Notes = "with friends",
    };

    [Fact]
    public async Task A_plan_is_drafted_in_his_words_and_units_and_nothing_is_written()
    {
        var squat = await _desk.Athletes.PlantMovementAsync(Owner, "Hack Squat");

        var draft = ResultAssert.Succeeded(await _desk.DraftAsync(drafter => drafter.PlanSessionAsync(_desk.Athletes.Owner, new()
        {
            Day = NextWednesday,
            Lifts = [new PlannedLift { MovementDataId = squat, Sets = 3, Reps = 8, WeightLb = 230, Rpe = 8 }],
            Freeform = ["Easy run, 4 miles"],
            Notes = "with friends",
        }, default)));

        Assert.Equal("Plan a session on Wed 11 Nov", draft.Summary.Title);
        Assert.Equal(["Hack Squat (Barbell): 3 x 8 at 230 lb, RPE 8", "Easy run, 4 miles", "Note: with friends"], draft.Summary.Lines);
        var plan = Assert.IsType<ProposedChange.PlanSession>(draft.Change).Plan;
        Assert.Equal((NextWednesday, 4, "with friends"), (plan.PlannedFor, plan.Prescriptions.Count, plan.Notes));
        Assert.Equal(0, await _desk.SessionsStoredAsync());
    }

    [Fact]
    public async Task A_plan_for_a_day_gone_by_is_refused_naming_the_day()
    {
        var problem = ResultAssert.Failed(await _desk.DraftAsync(drafter =>
            drafter.PlanSessionAsync(_desk.Athletes.Owner, ARun(ChangeDesk.Today.AddDays(-1)), default)));

        Assert.Equal("Day", Assert.Single(Assert.IsType<Problem.Invalid>(problem).Fields).Field);
    }

    [Fact]
    public async Task A_plan_for_today_is_drafted()
    {
        ResultAssert.Succeeded(await _desk.DraftAsync(drafter =>
            drafter.PlanSessionAsync(_desk.Athletes.Owner, ARun(ChangeDesk.Today), default)));
    }

    [Fact]
    public async Task A_lift_on_another_athletes_movement_is_refused()
    {
        var theirs = await _desk.Athletes.PlantMovementAsync(_desk.Athletes.StrangerId, "Hack Squat");

        var problem = ResultAssert.Failed(await _desk.DraftAsync(drafter => drafter.PlanSessionAsync(_desk.Athletes.Owner, new()
        {
            Day = NextWednesday,
            Lifts = [new PlannedLift { MovementDataId = theirs, Sets = 3, Reps = 8, WeightLb = 230 }],
        }, default)));

        Assert.IsType<Problem.NotFound>(problem);
    }

    [Fact]
    public async Task A_session_he_planned_himself_is_not_the_chats_to_change_or_remove()
    {
        var his = ResultAssert.Succeeded(await _desk.Athletes.SessionsAsync(service => service.PlanAsync(
            _desk.Athletes.Owner, new PlanSessionRequest(NextWednesday, [new FreeformPrescriptionCommand("Long run")], "", null), default)));

        var change = ResultAssert.Failed(await _desk.DraftAsync(drafter => drafter.RevisePlanAsync(_desk.Athletes.Owner,
            new PlanChangeArguments { TrainingSessionId = his.TrainingSessionId, Plan = ARun(NextWednesday) }, default)));
        var removal = ResultAssert.Failed(await _desk.DraftAsync(drafter =>
            drafter.RemovePlanAsync(_desk.Athletes.Owner, his.TrainingSessionId, default)));

        Assert.IsType<Problem.Forbidden>(change);
        Assert.IsType<Problem.Forbidden>(removal);
    }

    [Fact]
    public async Task A_change_to_a_session_Tempo_planned_says_where_it_moves_and_what_it_replaces()
    {
        var planned = ResultAssert.Succeeded(await _desk.Athletes.SessionsAsync(service => service.PlanAssistedAsync(
            _desk.Athletes.Owner, new PlanSessionRequest(NextWednesday, [new FreeformPrescriptionCommand("Easy run")], "", null), default)));

        var draft = ResultAssert.Succeeded(await _desk.DraftAsync(drafter => drafter.RevisePlanAsync(_desk.Athletes.Owner,
            new PlanChangeArguments
            {
                TrainingSessionId = planned.TrainingSessionId,
                Plan = new PlannedSessionArguments { Day = NextWednesday.AddDays(1), Freeform = ["Tempo run, 3 miles"] },
            }, default)));

        Assert.Equal("Change the session planned for Wed 11 Nov", draft.Summary.Title);
        Assert.Equal(["Moves to Thu 12 Nov", "Tempo run, 3 miles", "Replaces: Easy run"], draft.Summary.Lines);
        Assert.Equal(planned.TrainingSessionId, Assert.IsType<ProposedChange.RevisePlan>(draft.Change).TrainingSessionId);
    }

    [Fact]
    public async Task A_session_no_longer_planned_cannot_be_changed()
    {
        var planned = ResultAssert.Succeeded(await _desk.Athletes.SessionsAsync(service => service.PlanAssistedAsync(
            _desk.Athletes.Owner, new PlanSessionRequest(NextWednesday, [new FreeformPrescriptionCommand("Easy run")], "", null), default)));
        ResultAssert.Succeeded(await _desk.Athletes.SessionsAsync(service => service.SkipAsync(_desk.Athletes.Owner, planned.TrainingSessionId, default)));

        var problem = ResultAssert.Failed(await _desk.DraftAsync(drafter =>
            drafter.RemovePlanAsync(_desk.Athletes.Owner, planned.TrainingSessionId, default)));

        Assert.IsType<Problem.Conflict>(problem);
    }

    [Fact]
    public async Task A_day_already_marked_is_refused_before_he_sees_a_card()
    {
        ResultAssert.Succeeded(await _desk.Athletes.NonTrainingDaysAsync(ChangeDesk.Clock, service =>
            service.MarkAsync(_desk.Athletes.Owner, new MarkNonTrainingDayRequest(NextWednesday), default)));

        var problem = ResultAssert.Failed(await _desk.DraftAsync(drafter => drafter.MarkNonTrainingDayAsync(
            _desk.Athletes.Owner, new NonTrainingDayArguments { Day = NextWednesday, Note = "travel" }, default)));

        Assert.Equal(new Problem.Conflict("That day is already marked non-training."), problem);
    }

    [Fact]
    public async Task A_weekly_day_off_reads_as_its_weekday_with_its_span()
    {
        var draft = ResultAssert.Succeeded(await _desk.DraftAsync(drafter => drafter.MarkNonTrainingDayAsync(
            _desk.Athletes.Owner,
            new NonTrainingDayArguments { Day = NextWednesday, Repeat = "weekly", Until = NextWednesday.AddDays(21), Note = " exams " },
            default)));

        Assert.Equal("Mark every Wednesday as non-training", draft.Summary.Title);
        Assert.Equal(["From Wed 11 Nov until Wed 2 Dec", "Note: exams"], draft.Summary.Lines);
    }

    [Fact]
    public async Task Freeing_a_weekly_day_says_every_week_it_covered_goes()
    {
        var marked = ResultAssert.Succeeded(await _desk.Athletes.NonTrainingDaysAsync(ChangeDesk.Clock, service =>
            service.MarkAsync(_desk.Athletes.Owner, new MarkNonTrainingDayRequest(NextWednesday, NonTrainingRepeat.Weekly, Note: "class"), default)));

        var draft = ResultAssert.Succeeded(await _desk.DraftAsync(drafter =>
            drafter.FreeNonTrainingDayAsync(_desk.Athletes.Owner, marked.NonTrainingDayId, default)));

        Assert.Equal("Free every Wednesday for training", draft.Summary.Title);
        Assert.Equal(["From Wed 11 Nov, with no last day", "Note: class"], draft.Summary.Lines);
    }

    [Fact]
    public async Task A_new_priority_names_the_lift_it_lets_go()
    {
        var dip = await _desk.Athletes.PlantMovementAsync(Owner, "Dip");
        var bench = await _desk.Athletes.PlantMovementAsync(Owner, "Bench Press");
        ResultAssert.Succeeded(await _desk.Athletes.FocusAsync(ChangeDesk.Clock, service => service.PinAsync(_desk.Athletes.Owner,
            new PinFocusRequest(dip, ChangeDesk.Today, ChangeDesk.Today.AddDays(42), 2, null, PatternFamily.Push), default)));

        var draft = ResultAssert.Succeeded(await _desk.DraftAsync(drafter => drafter.PinPriorityLiftAsync(_desk.Athletes.Owner,
            new PriorityLiftArguments
            {
                Family = "push", MovementDataId = bench, Ends = ChangeDesk.Today.AddDays(41), SessionsPerWeek = 2,
                TargetWeightLb = 225, TargetReps = 5,
            }, default)));

        Assert.Equal("Make Bench Press (Barbell) the push priority", draft.Summary.Title);
        Assert.Equal(
            ["Block: Wed 4 Nov to Tue 15 Dec", "2 sessions a week", "Target: 225 lb x 5", "Lets go of Dip (Barbell) as the push priority"],
            draft.Summary.Lines);
    }

    [Fact]
    public async Task A_priority_that_trains_another_family_is_refused()
    {
        var row = await _desk.Athletes.PlantMovementAsync(Owner, "Seated Row");

        var problem = ResultAssert.Failed(await _desk.DraftAsync(drafter => drafter.PinPriorityLiftAsync(_desk.Athletes.Owner,
            new PriorityLiftArguments { Family = "push", MovementDataId = row, Ends = ChangeDesk.Today.AddDays(28), SessionsPerWeek = 2 },
            default)));

        Assert.Equal("Priority", Assert.Single(Assert.IsType<Problem.Invalid>(problem).Fields).Field);
    }

    [Fact]
    public async Task A_check_in_on_another_athletes_injury_is_refused()
    {
        var theirs = Guid.NewGuid();
        await _desk.Athletes.PlantAsync(context => context.Injuries.Add(new Injury
        {
            InjuryID = theirs, UserID = _desk.Athletes.StrangerId, Name = "knee", Notes = "", InjuryDate = ChangeDesk.Today,
        }));

        var problem = ResultAssert.Failed(await _desk.DraftAsync(drafter => drafter.LogInjuryCheckInAsync(
            _desk.Athletes.Owner, new InjuryCheckInArguments { InjuryId = theirs, PainLevel = 3 }, default)));

        Assert.IsType<Problem.NotFound>(problem);
    }

    [Fact]
    public async Task An_injury_that_has_not_happened_yet_is_refused()
    {
        var problem = ResultAssert.Failed(await _desk.DraftAsync(drafter => drafter.LogInjuryAsync(
            new InjuryArguments { Name = "left knee", Day = ChangeDesk.Today.AddDays(1) })));

        Assert.Equal("Day", Assert.Single(Assert.IsType<Problem.Invalid>(problem).Fields).Field);
    }
}
