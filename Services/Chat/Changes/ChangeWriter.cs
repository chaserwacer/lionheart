using System.Diagnostics;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Chat.Changes;
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
public sealed class ChangeWriter(
    ModelContext context,
    ITrainingSessionService sessions,
    INonTrainingDayService nonTrainingDays,
    IFocusPinService focus,
    IInjuryService injuries)
{
    private readonly ModelContext _context = context;
    private readonly ITrainingSessionService _sessions = sessions;
    private readonly INonTrainingDayService _nonTrainingDays = nonTrainingDays;
    private readonly IFocusPinService _focus = focus;
    private readonly IInjuryService _injuries = injuries;

    /// <summary>Writes the change, or answers with why its service refused it.</summary>
    public Task<Result<Unit, Problem>> WriteAsync(IdentityUser user, ProposedChange change, CancellationToken ct) => change switch
    {
        ProposedChange.PlanSession plan => _sessions.PlanAssistedAsync(user, plan.Plan, ct).MapAsync(_ => Unit.Value),
        ProposedChange.RevisePlan revise => StillTempoPlannedAsync(user, revise.TrainingSessionId, ct)
            .BindAsync(_ => _sessions.RevisePlanAsync(user, revise.TrainingSessionId, revise.Plan, ct))
            .MapAsync(_ => Unit.Value),
        ProposedChange.RemovePlan remove => StillTempoPlannedAsync(user, remove.TrainingSessionId, ct)
            .BindAsync(_ => _sessions.DeleteAsync(user, remove.TrainingSessionId, ct)),
        ProposedChange.MarkNonTrainingDay mark => _nonTrainingDays.MarkAsync(user, mark.Request, ct).MapAsync(_ => Unit.Value),
        ProposedChange.FreeNonTrainingDay free => _nonTrainingDays.RemoveAsync(user, free.NonTrainingDayId, ct),
        ProposedChange.PinPriorityLift pin => _focus.PinAsync(user, pin.Request, ct).MapAsync(_ => Unit.Value),
        ProposedChange.LogInjury injury => _injuries.CreateInjuryAsync(user, injury.Request).MapAsync(_ => Unit.Value),
        ProposedChange.LogInjuryCheckIn checkIn => _injuries.CreateInjuryEventAsync(user, checkIn.Request).MapAsync(_ => Unit.Value),
        _ => throw new UnreachableException(),
    };

    /// <summary>
    /// Asked again at confirmation: the session may have been trained since the card was drawn, and
    /// the session service's delete would take a trained session with it.
    /// </summary>
    private Task<Result<Unit, Problem>> StillTempoPlannedAsync(IdentityUser user, Guid trainingSessionId, CancellationToken ct) =>
        _context.PlannedByTempoAsync(Guid.Parse(user.Id), trainingSessionId, ct).MapAsync(_ => Unit.Value);
}
