using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Chat.Changes;
using lionheart.Model.InjuryManagement;
using lionheart.Model.Training.Availability;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Chat.Today;
using lionheart.Services.Training.Movement;
using lionheart.Services.Training.Patterns;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Chat.Changes;

/// <summary>
/// Turns what the model asked for into a proposal the athlete can answer: checked against his data
/// as far as a read can check it, and summarised in words resolved from that data. Writes nothing.
/// </summary>
/// <remarks>
/// Every refusal here goes back to the model as a failed tool call, so it says what to do about it.
/// The service that makes the write checks again when the athlete confirms; these checks exist so a
/// card he is shown is one that will go through, and so the model can put a bad call right before he
/// sees anything.
/// </remarks>
public sealed class ChangeDrafter(ModelContext context, TimeProvider clock, MovementPatternMap map)
{
    private readonly ModelContext _context = context;
    private readonly TimeProvider _clock = clock;
    private readonly MovementPatternMap _map = map;

    public Task<Result<ProposalDraft, Problem>> PlanSessionAsync(IdentityUser user, PlannedSessionArguments arguments, CancellationToken ct) =>
        StatedChanges.Plan(arguments)
            .Bind(NotBeforeToday)
            .BindAsync(plan => PrescribedAsync(user, plan.Prescriptions, [], ct)
                .MapAsync(prescribed => Drafted(
                    new ProposedChange.PlanSession(plan.ToPlanRequest()),
                    ChangeSummaries.PlanSession(plan.ToPlanRequest(), prescribed.Prescriptions, prescribed.Names))));

    public Task<Result<ProposalDraft, Problem>> RevisePlanAsync(IdentityUser user, PlanChangeArguments arguments, CancellationToken ct) =>
        Present(arguments.TrainingSessionId, nameof(arguments.TrainingSessionId), "Name the planned session by its trainingSessionId.")
            .BindAsync(sessionId => _context.PlannedByTempoAsync(Guid.Parse(user.Id), sessionId, ct))
            .BindAsync(before => StatedChanges.Plan(arguments.Plan)
                .Bind(NotBeforeToday)
                .BindAsync(plan => PrescribedAsync(user, plan.Prescriptions, before.Prescriptions, ct)
                    .MapAsync(prescribed => Drafted(
                        new ProposedChange.RevisePlan(before.TrainingSessionId, plan.ToReviseRequest()),
                        ChangeSummaries.RevisePlan(before, plan.ToReviseRequest(), prescribed.Prescriptions, prescribed.Names)))));

    public Task<Result<ProposalDraft, Problem>> RemovePlanAsync(IdentityUser user, Guid trainingSessionId, CancellationToken ct) =>
        _context.PlannedByTempoAsync(Guid.Parse(user.Id), trainingSessionId, ct)
            .BindAsync(planned => PrescribedAsync(user, [], planned.Prescriptions, ct)
                .MapAsync(prescribed => Drafted(
                    new ProposedChange.RemovePlan(planned.TrainingSessionId),
                    ChangeSummaries.RemovePlan(planned, prescribed.Names))));

    public Task<Result<ProposalDraft, Problem>> MarkNonTrainingDayAsync(IdentityUser user, NonTrainingDayArguments arguments, CancellationToken ct) =>
        StatedChanges.NonTrainingDay(arguments)
            .BindAsync(request => request.ToSchedule()
                .BindAsync(schedule => NotAlreadyMarkedAsync(user, schedule, ct))
                .MapAsync(schedule => Drafted(
                    new ProposedChange.MarkNonTrainingDay(request),
                    ChangeSummaries.MarkNonTrainingDay(schedule, request.Note?.Trim() ?? string.Empty))));

    public Task<Result<ProposalDraft, Problem>> FreeNonTrainingDayAsync(IdentityUser user, Guid nonTrainingDayId, CancellationToken ct) =>
        MarkedAsync(user, nonTrainingDayId, ct)
            .MapAsync(marked => Drafted(
                new ProposedChange.FreeNonTrainingDay(marked.NonTrainingDayId),
                ChangeSummaries.FreeNonTrainingDay(marked)));

    public Task<Result<ProposalDraft, Problem>> PinPriorityLiftAsync(IdentityUser user, PriorityLiftArguments arguments, CancellationToken ct) =>
        StatedChanges.PriorityLift(arguments, AthleteNow.Of(_clock).Today)
            .Bind(stated => stated.Request.ToRole()
                .Bind(role => stated.Request.ToTerms().Map(terms => new StatedPin(stated.Family, stated.Request, role, terms))))
            .BindAsync(pin => LiftForAsync(user, pin, ct))
            .MapAsync(pin => PinDraftAsync(user, pin, ct));

    public Task<Result<ProposalDraft, Problem>> LogInjuryAsync(InjuryArguments arguments) =>
        Task.FromResult(StatedChanges.Injury(arguments)
            .Ensure(
                request => DateOnly.FromDateTime(request.InjuryDate) <= AthleteNow.Of(_clock).Today,
                _ => new Problem.Invalid(nameof(InjuryArguments.Day), "An injury starts today or earlier."))
            .Map(request => Drafted(
                new ProposedChange.LogInjury(request),
                ChangeSummaries.LogInjury(request.Name, DateOnly.FromDateTime(request.InjuryDate), request.Notes))));

    public Task<Result<ProposalDraft, Problem>> LogInjuryCheckInAsync(IdentityUser user, InjuryCheckInArguments arguments, CancellationToken ct) =>
        StatedChanges.InjuryCheckIn(arguments)
            .BindAsync(request => InjuryNameAsync(user, request.InjuryID, ct)
                .BindAsync(injury => HisMovementsAsync(user, request.MovementIDs, [], ct)
                    .MapAsync(names => Drafted(
                        new ProposedChange.LogInjuryCheckIn(request),
                        ChangeSummaries.LogInjuryCheckIn(
                            injury, request.PainLevel, request.InjuryType == InjuryEventType.treatment, request.Notes,
                            [.. request.MovementIDs.Select(names.NameOf)])))));

    // ---- Checks against his data ----

    private Result<StatedPlan, Problem> NotBeforeToday(StatedPlan plan) =>
        plan.Day >= AthleteNow.Of(_clock).Today
            ? Result<StatedPlan, Problem>.Success(plan)
            : Result<StatedPlan, Problem>.Failure(new Problem.Invalid(
                nameof(PlannedSessionArguments.Day), "A session is planned for today or a later day; the TODAY note has the date."));

    /// <summary>
    /// The new prescriptions as the session will hold them, and the names of every movement either
    /// plan mentions — each new one has to be his.
    /// </summary>
    private Task<Result<Prescribed, Problem>> PrescribedAsync(
        IdentityUser user, IReadOnlyList<SetPrescriptionCommand> stated, IReadOnlyList<SetPrescription> existing, CancellationToken ct) =>
        stated.Traverse(command => command.ToPrescription())
            .BindAsync(prescriptions => HisMovementsAsync(user, MovementNameBook.MovementsOf(prescriptions), MovementNameBook.MovementsOf(existing), ct)
                .MapAsync(names => new Prescribed(prescriptions, names)));

    /// <summary>The names of the movements named, refusing any of <paramref name="his"/> that is not one of his.</summary>
    private async Task<Result<MovementNameBook, Problem>> HisMovementsAsync(
        IdentityUser user, IReadOnlyList<Guid> his, IReadOnlyList<Guid> alsoNamed, CancellationToken ct) =>
        AllHis(await _context.MovementNamesAsync(Guid.Parse(user.Id), [.. his, .. alsoNamed], ct), his);

    private static Result<MovementNameBook, Problem> AllHis(MovementNameBook names, IReadOnlyList<Guid> his) =>
        his.Where(id => !names.Knows(id)).Select(id => (Guid?)id).FirstOrDefault() is { } missing
            ? Result<MovementNameBook, Problem>.Failure(new Problem.NotFound(
                $"No movement of his has the id {missing}; FindMovements gives the ids of his movements."))
            : Result<MovementNameBook, Problem>.Success(names);

    /// <summary>
    /// The same schedule marked twice is refused by the page's service, so it is refused here first,
    /// where the model can say so rather than offering a card that will fail.
    /// </summary>
    private async Task<Result<NonTrainingSchedule, Problem>> NotAlreadyMarkedAsync(
        IdentityUser user, NonTrainingSchedule schedule, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var marked = await _context.NonTrainingDays.AsNoTracking().Where(day => day.UserId == userId).ToListAsync(ct);
        return marked.Any(day => day.Schedule == schedule)
            ? Result<NonTrainingSchedule, Problem>.Failure(new Problem.Conflict("That day is already marked non-training."))
            : Result<NonTrainingSchedule, Problem>.Success(schedule);
    }

    private async Task<Result<NonTrainingDay, Problem>> MarkedAsync(IdentityUser user, Guid nonTrainingDayId, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var marked = await _context.NonTrainingDays.AsNoTracking()
            .FirstOrDefaultAsync(day => day.NonTrainingDayId == nonTrainingDayId && day.UserId == userId, ct);
        return marked is null
            ? Result<NonTrainingDay, Problem>.Failure(new Problem.NotFound(
                "No non-training day of his has that id; GetPlannedDays lists them with their ids."))
            : Result<NonTrainingDay, Problem>.Success(marked);
    }

    /// <summary>The lift, if it is his and trains the family it is to lead.</summary>
    private async Task<Result<StatedPin, Problem>> LiftForAsync(IdentityUser user, StatedPin pin, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var movement = await _context.MovementDatas
            .AsNoTracking()
            .Include(m => m.MovementBase)
            .Include(m => m.Equipment)
            .Include(m => m.MovementModifier)
            .FirstOrDefaultAsync(m => m.MovementDataID == pin.Request.MovementDataId && m.UserID == userId, ct);
        if (movement is null)
            return Result<StatedPin, Problem>.Failure(new Problem.NotFound(
                "No movement of his has that id; FindMovements gives the ids of his movements."));

        var name = MovementNames.Of(movement.MovementBase.Name, movement.Equipment.Name, movement.MovementModifier?.Name);
        return pin.Role.Admits(name, _map.StandingOf(movement.MovementBase)).Map(_ => pin with { Lift = name });
    }

    /// <summary>The card for a priority pin, naming the lift that leads the family now, which the new pin lets go.</summary>
    private async Task<ProposalDraft> PinDraftAsync(IdentityUser user, StatedPin pin, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var leading = (await _context.FocusPins.AsNoTracking().Where(other => other.UserId == userId).ToListAsync(ct))
            .FirstOrDefault(other => other.IsPinned && other.Role.IsPriorityFor(pin.Family));
        var superseded = leading is null
            ? null
            : (await _context.MovementNamesAsync(userId, [leading.MovementDataId], ct)).NameOf(leading.MovementDataId);

        return Drafted(
            new ProposedChange.PinPriorityLift(pin.Request),
            ChangeSummaries.PinPriorityLift(pin.Family, pin.Lift, pin.Terms, superseded));
    }

    private async Task<Result<string, Problem>> InjuryNameAsync(IdentityUser user, Guid injuryId, CancellationToken ct)
    {
        var userId = Guid.Parse(user.Id);
        var name = await _context.Injuries.AsNoTracking()
            .Where(injury => injury.InjuryID == injuryId && injury.UserID == userId)
            .Select(injury => injury.Name)
            .FirstOrDefaultAsync(ct);
        return name is null
            ? Result<string, Problem>.Failure(new Problem.NotFound(
                "No injury of his has that id; GetAllUserInjuries lists them with their ids."))
            : Result<string, Problem>.Success(name);
    }

    // ---- The draft ----

    private ProposalDraft Drafted(ProposedChange change, ProposalSummary summary) =>
        new(Guid.CreateVersion7(_clock.GetUtcNow()), change, summary);

    private static Result<T, Problem> Present<T>(T? value, string field, string message) where T : struct =>
        value is { } present
            ? Result<T, Problem>.Success(present)
            : Result<T, Problem>.Failure(new Problem.Invalid(field, message));

    /// <summary>A plan's prescriptions as the session will hold them, and the names it is read with.</summary>
    private sealed record Prescribed(IReadOnlyList<SetPrescription> Prescriptions, MovementNameBook Names);

    /// <summary>A priority pin as stated, read into its role and terms, with the lift's name once it is found.</summary>
    private sealed record StatedPin(PatternFamily Family, PinFocusRequest Request, FocusRole Role, FocusTerms Terms)
    {
        public string Lift { get; init; } = string.Empty;
    }
}
