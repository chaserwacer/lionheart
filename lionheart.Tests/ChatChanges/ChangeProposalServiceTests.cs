using lionheart.Functional;
using lionheart.Model.Chat.Changes;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sessions;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.ChatChanges;

/// <summary>
/// The athlete's answer to a card: the only thing that writes a proposed change, through the page's
/// own service, once, and recorded with what became of it.
/// </summary>
public sealed class ChangeProposalServiceTests : IDisposable
{
    private static readonly DateOnly NextWednesday = ChangeDesk.Today.AddDays(7);

    private readonly ChangeDesk _desk = new();

    public void Dispose() => _desk.Dispose();

    private Task<Guid> ProposeRunAsync(DateOnly day) =>
        _desk.ProposeAsync(drafter => drafter.PlanSessionAsync(_desk.Athletes.Owner, new PlannedSessionArguments
        {
            Day = day,
            Freeform = ["Easy run, 4 miles"],
            Notes = "with friends",
        }, default));

    private Task<Result<ChangeProposalView, Problem>> ConfirmAsync(Guid proposalId) =>
        _desk.ProposalsAsync(service => service.ConfirmAsync(_desk.Athletes.Owner, proposalId, default));

    private Task<Result<ChangeProposalView, Problem>> DeclineAsync(Guid proposalId) =>
        _desk.ProposalsAsync(service => service.DeclineAsync(_desk.Athletes.Owner, proposalId, default));

    [Fact]
    public async Task Confirming_a_plan_writes_it_as_planned_by_Tempo_and_settles_the_card()
    {
        var proposalId = await ProposeRunAsync(NextWednesday);

        var confirmed = ResultAssert.Succeeded(await ConfirmAsync(proposalId));

        Assert.Equal(new ProposalAnswer.Confirmed(ChangeDesk.Now.UtcDateTime), confirmed.Answer);
        var planned = Assert.IsType<PlannedTrainingSession>(Assert.Single(await _desk.ReadAsync(context => context.Sessions.ToListAsync())));
        Assert.Equal((NextWednesday, Provenance.AiAssisted, "with friends"), (planned.PlannedFor, planned.Provenance, planned.Notes));
        Assert.Equal(new FreeformPrescription("Easy run, 4 miles"), Assert.Single(planned.Prescriptions));
    }

    [Fact]
    public async Task Declining_writes_nothing_and_settles_the_card()
    {
        var proposalId = await ProposeRunAsync(NextWednesday);

        var declined = ResultAssert.Succeeded(await DeclineAsync(proposalId));

        Assert.Equal(new ProposalAnswer.Declined(ChangeDesk.Now.UtcDateTime), declined.Answer);
        Assert.Equal(0, await _desk.SessionsStoredAsync());
    }

    [Fact]
    public async Task A_card_answers_once_and_its_change_is_written_once()
    {
        var proposalId = await ProposeRunAsync(NextWednesday);
        ResultAssert.Succeeded(await ConfirmAsync(proposalId));

        var again = ResultAssert.Failed(await ConfirmAsync(proposalId));
        var declined = ResultAssert.Failed(await DeclineAsync(proposalId));

        Assert.Equal(new Problem.Conflict("That change has already been answered."), again);
        Assert.Equal(new Problem.Conflict("That change has already been answered."), declined);
        Assert.Equal(1, await _desk.SessionsStoredAsync());
    }

    [Fact]
    public async Task A_write_its_service_refuses_settles_as_refused_with_the_services_reason()
    {
        var proposalId = await _desk.ProposeAsync(drafter => drafter.MarkNonTrainingDayAsync(
            _desk.Athletes.Owner, new NonTrainingDayArguments { Day = NextWednesday, Note = "travel" }, default));
        // Marked on Train between the card being drawn and the athlete tapping it.
        ResultAssert.Succeeded(await _desk.Athletes.NonTrainingDaysAsync(ChangeDesk.Clock, service =>
            service.MarkAsync(_desk.Athletes.Owner, new MarkNonTrainingDayRequest(NextWednesday), default)));

        var refused = ResultAssert.Succeeded(await ConfirmAsync(proposalId));

        Assert.Equal(new ProposalAnswer.Refused(ChangeDesk.Now.UtcDateTime, "That day is already marked non-training."), refused.Answer);
        Assert.Equal(1, await _desk.ReadAsync(context => context.NonTrainingDays.CountAsync()));
    }

    [Fact]
    public async Task Removing_a_plan_he_has_since_started_is_refused_and_the_session_is_kept()
    {
        var planned = ResultAssert.Succeeded(await _desk.Athletes.SessionsAsync(service => service.PlanAssistedAsync(
            _desk.Athletes.Owner, new PlanSessionRequest(ChangeDesk.Today, [new FreeformPrescriptionCommand("Easy run")], "", null), default)));
        var proposalId = await _desk.ProposeAsync(drafter =>
            drafter.RemovePlanAsync(_desk.Athletes.Owner, planned.TrainingSessionId, default));
        ResultAssert.Succeeded(await _desk.Athletes.SessionsAsync(service => service.StartAsync(
            _desk.Athletes.Owner, planned.TrainingSessionId, new StartSessionRequest(ChangeDesk.Now), default)));

        var answer = ResultAssert.Succeeded(await ConfirmAsync(proposalId)).Answer;

        Assert.IsType<ProposalAnswer.Refused>(answer);
        Assert.IsType<ActiveTrainingSession>(Assert.Single(await _desk.ReadAsync(context => context.Sessions.ToListAsync())));
    }

    [Fact]
    public async Task A_confirmed_change_to_a_plan_moves_it_and_keeps_it_Tempos()
    {
        var planned = ResultAssert.Succeeded(await _desk.Athletes.SessionsAsync(service => service.PlanAssistedAsync(
            _desk.Athletes.Owner, new PlanSessionRequest(NextWednesday, [new FreeformPrescriptionCommand("Easy run")], "", null), default)));
        var proposalId = await _desk.ProposeAsync(drafter => drafter.RevisePlanAsync(_desk.Athletes.Owner, new PlanChangeArguments
        {
            TrainingSessionId = planned.TrainingSessionId,
            Plan = new PlannedSessionArguments { Day = NextWednesday.AddDays(1), Freeform = ["Easy run"], Notes = "moved" },
        }, default));

        ResultAssert.Succeeded(await ConfirmAsync(proposalId));

        var moved = Assert.IsType<PlannedTrainingSession>(Assert.Single(await _desk.ReadAsync(context => context.Sessions.ToListAsync())));
        Assert.Equal((planned.TrainingSessionId, NextWednesday.AddDays(1), "moved", Provenance.AiAssisted),
            (moved.TrainingSessionId, moved.PlannedFor, moved.Notes, moved.Provenance));
    }

    [Fact]
    public async Task A_confirmed_priority_lets_the_last_one_go_as_the_focus_page_would()
    {
        var dip = await _desk.Athletes.PlantMovementAsync(_desk.Athletes.OwnerId, "Dip");
        var bench = await _desk.Athletes.PlantMovementAsync(_desk.Athletes.OwnerId, "Bench Press");
        ResultAssert.Succeeded(await _desk.Athletes.FocusAsync(ChangeDesk.Clock, service => service.PinAsync(_desk.Athletes.Owner,
            new PinFocusRequest(dip, ChangeDesk.Today.AddDays(-14), ChangeDesk.Today.AddDays(28), 2, null, PatternFamily.Push), default)));
        var proposalId = await _desk.ProposeAsync(drafter => drafter.PinPriorityLiftAsync(_desk.Athletes.Owner,
            new PriorityLiftArguments { Family = "push", MovementDataId = bench, Ends = ChangeDesk.Today.AddDays(42), SessionsPerWeek = 2 },
            default));

        ResultAssert.Succeeded(await ConfirmAsync(proposalId));

        var pinned = await _desk.ReadAsync(context => context.FocusPins.ToListAsync());
        Assert.Equal(bench, Assert.Single(pinned, pin => pin.IsPinned).MovementDataId);
        Assert.False(Assert.Single(pinned, pin => pin.MovementDataId == dip).IsPinned);
    }

    [Fact]
    public async Task A_confirmed_injury_and_check_in_land_on_the_injury_log()
    {
        var injuryProposal = await _desk.ProposeAsync(drafter => drafter.LogInjuryAsync(
            new InjuryArguments { Name = "left knee", Day = ChangeDesk.Today, Notes = "sore on stairs" }));
        ResultAssert.Succeeded(await ConfirmAsync(injuryProposal));
        var injury = Assert.Single(await _desk.ReadAsync(context => context.Injuries.ToListAsync()));

        var checkIn = await _desk.ProposeAsync(drafter => drafter.LogInjuryCheckInAsync(
            _desk.Athletes.Owner, new InjuryCheckInArguments { InjuryId = injury.InjuryID, PainLevel = 3, Notes = "better" }, default));
        ResultAssert.Succeeded(await ConfirmAsync(checkIn));

        Assert.Equal(("left knee", ChangeDesk.Today, true), (injury.Name, injury.InjuryDate, injury.IsActive));
        var logged = Assert.Single(await _desk.ReadAsync(context => context.InjuryEvents.ToListAsync()));
        Assert.Equal((3, "better"), (logged.PainLevel, logged.Notes));
    }

    [Fact]
    public async Task Another_athletes_card_cannot_be_answered()
    {
        var proposalId = await ProposeRunAsync(NextWednesday);

        var confirm = ResultAssert.Failed(await _desk.ProposalsAsync(service =>
            service.ConfirmAsync(_desk.Athletes.Stranger, proposalId, default)));
        var decline = ResultAssert.Failed(await _desk.ProposalsAsync(service =>
            service.DeclineAsync(_desk.Athletes.Stranger, proposalId, default)));

        Assert.IsType<Problem.NotFound>(confirm);
        Assert.IsType<Problem.NotFound>(decline);
        Assert.Equal(0, await _desk.SessionsStoredAsync());
    }

    [Fact]
    public async Task A_proposal_reads_back_whole_from_storage()
    {
        var squat = await _desk.Athletes.PlantMovementAsync(_desk.Athletes.OwnerId, "Hack Squat");
        var draft = ResultAssert.Succeeded(await _desk.DraftAsync(drafter => drafter.PlanSessionAsync(_desk.Athletes.Owner, new()
        {
            Day = NextWednesday,
            Lifts = [new PlannedLift { MovementDataId = squat, Sets = 2, Reps = 5, WeightLb = 250.5, Rpe = 8.5 }],
        }, default)));
        await _desk.StoreAsync(draft);

        var stored = Assert.Single(await _desk.ReadAsync(context => context.ChangeProposals.AsNoTracking().ToListAsync()));

        var plan = Assert.IsType<ProposedChange.PlanSession>(stored.Change).Plan;
        Assert.Equal(
            Assert.IsType<ProposedChange.PlanSession>(draft.Change).Plan.Prescriptions,
            plan.Prescriptions);
        Assert.Equal(draft.Summary.Title, stored.Summary.Title);
        Assert.Equal(draft.Summary.Lines, stored.Summary.Lines);
        Assert.IsType<ProposalAnswer.Waiting>(stored.Answer);
    }

    /// <summary>
    /// Two taps racing both pass the guard; the answer being a concurrency token is what stops the
    /// second from settling, and so from keeping a second write.
    /// </summary>
    [Fact]
    public async Task A_second_settle_racing_the_first_finds_the_card_already_answered()
    {
        var proposalId = await ProposeRunAsync(NextWednesday);
        await using var first = _desk.Athletes.CreateContext();
        await using var second = _desk.Athletes.CreateContext();
        var firstTap = await first.ChangeProposals.SingleAsync(p => p.ChangeProposalId == proposalId);
        var secondTap = await second.ChangeProposals.SingleAsync(p => p.ChangeProposalId == proposalId);

        ResultAssert.Succeeded(firstTap.Settle(new ProposalAnswer.Confirmed(ChangeDesk.Now.UtcDateTime)));
        await first.SaveChangesAsync();
        ResultAssert.Succeeded(secondTap.Settle(new ProposalAnswer.Declined(ChangeDesk.Now.UtcDateTime)));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
