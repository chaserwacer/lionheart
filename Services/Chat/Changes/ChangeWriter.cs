using System.Diagnostics;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Chat.Changes;
using lionheart.Model.Training.Patterns;
using lionheart.Services.Training.Availability;
using lionheart.Services.Training.Focus;
using lionheart.Services.Training.Sessions;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services.Chat.Changes;

/// <summary>
/// Makes a confirmed change through the service the page that makes it uses, so the page's
/// validation applies and everything that reads what it writes — availability, the week's targets,
/// the briefing — sees it with no further step.
/// </summary>
/// <remarks>
/// What the card took for granted is asked again first, because a card can be answered long after it
/// was drawn: the day it plans for may have passed, and the session or priority it replaces may have
/// been changed on a page. A confirmation that would write something other than what the card said
/// is refused, and the card says why.
/// </remarks>
public sealed class ChangeWriter(
    ModelContext context,
    ITrainingSessionService sessions,
    INonTrainingDayService nonTrainingDays,
    IFocusPinService focus,
    IInjuryService injuries,
    TimeProvider clock)
{
    private readonly ModelContext _context = context;
    private readonly TimeProvider _clock = clock;
    private readonly ITrainingSessionService _sessions = sessions;
    private readonly INonTrainingDayService _nonTrainingDays = nonTrainingDays;
    private readonly IFocusPinService _focus = focus;
    private readonly IInjuryService _injuries = injuries;

    /// <summary>Writes the change, or answers with why its service refused it.</summary>
    public Task<Result<Unit, Problem>> WriteAsync(IdentityUser user, ProposedChange change, CancellationToken ct) => change switch
    {
        ProposedChange.PlanSession plan => TempoPlans.NotBeforeToday(plan.Plan.PlannedFor, _clock)
            .BindAsync(_ => _sessions.PlanAssistedAsync(user, plan.Plan, ct))
            .MapAsync(_ => Unit.Value),
        ProposedChange.RevisePlan revise => StillAsDraftedAsync(user, revise.TrainingSessionId, revise.Replaces, ct)
            .BindAsync(_ => TempoPlans.NotBeforeToday(revise.Plan.PlannedFor, _clock))
            .BindAsync(_ => _sessions.RevisePlanAsync(user, revise.TrainingSessionId, revise.Plan, ct))
            .MapAsync(_ => Unit.Value),
        ProposedChange.RemovePlan remove => StillAsDraftedAsync(user, remove.TrainingSessionId, remove.Removes, ct)
            .BindAsync(_ => _sessions.DeleteAsync(user, remove.TrainingSessionId, ct)),
        ProposedChange.MarkNonTrainingDay mark => _nonTrainingDays.MarkAsync(user, mark.Request, ct).MapAsync(_ => Unit.Value),
        ProposedChange.FreeNonTrainingDay free => _nonTrainingDays.RemoveAsync(user, free.NonTrainingDayId, ct),
        ProposedChange.PinPriorityLift pin => StillLeadingAsync(user, pin, ct)
            .BindAsync(_ => _focus.PinAsync(user, pin.Request, ct))
            .MapAsync(_ => Unit.Value),
        ProposedChange.LogInjury injury => _injuries.CreateInjuryAsync(user, injury.Request).MapAsync(_ => Unit.Value),
        ProposedChange.LogInjuryCheckIn checkIn => _injuries.CreateInjuryEventAsync(user, checkIn.Request).MapAsync(_ => Unit.Value),
        _ => throw new UnreachableException(),
    };

    /// <summary>
    /// The session, still planned by Tempo and as the card showed it. Planned matters most: the
    /// session service's delete would take a session trained since with it.
    /// </summary>
    private Task<Result<Unit, Problem>> StillAsDraftedAsync(
        IdentityUser user, Guid trainingSessionId, PlanAsDrafted drafted, CancellationToken ct) =>
        _context.PlannedByTempoAsync(Guid.Parse(user.Id), trainingSessionId, ct)
            .BindAsync(planned => TempoPlans.AsDrafted(planned, drafted))
            .MapAsync(_ => Unit.Value);

    /// <summary>The family led by the same priorities the card said the new one lets go.</summary>
    private async Task<Result<Unit, Problem>> StillLeadingAsync(
        IdentityUser user, ProposedChange.PinPriorityLift pin, CancellationToken ct) =>
        (await _context.PinnedPrioritiesAsync(Guid.Parse(user.Id), pin.Family, ct))
            .Select(leading => leading.FocusPinId)
            .SequenceEqual(pin.LetsGo.Order())
            ? Result<Unit, Problem>.Success(Unit.Value)
            : Result<Unit, Problem>.Failure(new Problem.Conflict(
                $"The {PatternWords.Of(pin.Family)} priority changed after this card was drawn, so it no longer says what would be let go."));
}
