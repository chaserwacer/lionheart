using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.InjuryManagement;
using lionheart.Model.Training;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.Routines;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Import;
using lionheart.Services.Training.Patterns;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Movement.Library;

/// <summary>
/// Brings one athlete's movement library to the convention in <c>movement-library.json</c>:
/// renames, merges, retirements and the seed list, as one all-or-nothing change.
/// </summary>
/// <remarks>
/// Guard, plan, stage, replay, report — all inside one transaction, which a dry run rolls back, so
/// the dry run is the real run and what it prints is exactly what a write would do, down to the
/// record replay. The plan is checked whole before a row is touched (<see cref="TidyPlanner"/>);
/// the only refusal left to staging is a routine holding two movements that merge into one.
/// <para>
/// It saves twice inside the transaction. The first save repoints everything that names a merged
/// movement and removes the merged and retired ones; only then do the kept movements move onto
/// their renamed parts, so the unique index over a movement's parts never sees a kept movement
/// and the one it absorbed at once, and the emptied part rows go once nothing points at them.
/// </para>
/// </remarks>
public sealed class MovementLibraryTidy
{
    private readonly ModelContext _context;
    private readonly MovementLibraryRules _rules;
    private readonly PersonalRecordRebuild _records;
    private readonly MovementPatternMap _patterns;
    private readonly TimeProvider _clock;

    public MovementLibraryTidy(
        ModelContext context, MovementLibraryRules rules, PersonalRecordRebuild records, MovementPatternMap patterns,
        TimeProvider clock)
    {
        _context = context;
        _rules = rules;
        _records = records;
        _patterns = patterns;
        _clock = clock;
    }

    public Task<Result<TidyReport, Problem>> TidyAsync(Guid userId, TidyRun run, CancellationToken ct) =>
        _context.InTransactionAsync(
            () => _context.KnownAthleteAsync(userId, ct)
                .MapAsync(_ => HeldAsync(userId, ct))
                .BindAsync(held => TidyPlanner.Plan(held.State, _rules).Map(plan => new Planned(held, plan)))
                .BindAsync(planned => ReplayableAsync(userId, planned, ct))
                .BindAsync(planned => StageAsync(planned, ct))
                .BindAsync(planned => ReplayAsync(userId, planned, ct))
                .MapAsync(replayed => ReportAsync(userId, replayed, run, ct)),
            report => report.Written,
            ct);

    // ---- Guard ----

    /// <summary>The plan, if the records it would replay can be: a merge moves records, so a withdrawn one refuses it.</summary>
    private Task<Result<Planned, Problem>> ReplayableAsync(Guid userId, Planned planned, CancellationToken ct) =>
        planned.Plan.Merges.Count == 0
            ? Task.FromResult(Result<Planned, Problem>.Success(planned))
            : _records.ReplayableAsync(userId, ct).MapAsync(_ => planned);

    // ---- Reading the library ----

    private async Task<HeldLibrary> HeldAsync(Guid userId, CancellationToken ct)
    {
        var bases = await _context.MovementBases.Where(row => row.UserID == userId).ToListAsync(ct);
        var equipment = await _context.Equipments.Where(row => row.UserID == userId).ToListAsync(ct);
        var modifiers = await _context.MovementModifiers.Where(row => row.UserID == userId).ToListAsync(ct);
        var movements = await _context.MovementDatas
            .Where(movement => movement.UserID == userId)
            .Include(movement => movement.MovementBase)
            .Include(movement => movement.Equipment)
            .Include(movement => movement.MovementModifier)
            .ToListAsync(ct);

        var sessions = await _context.Sessions.Where(session => session.UserId == userId).ToListAsync(ct);
        var routines = await _context.Routines.Where(routine => routine.UserId == userId).ToListAsync(ct);
        var pins = await _context.FocusPins.Where(pin => pin.UserId == userId).ToListAsync(ct);
        var notes = await _context.InjuryEvents.Where(note => note.Injury.UserID == userId).ToListAsync(ct);
        var records = await _context.PersonalRecords.AsNoTracking().Where(record => record.UserId == userId).ToListAsync(ct);

        var uses = MovementUses.In(sessions, routines, pins, notes, records);

        return new HeldLibrary(
            userId,
            new LibraryState(
                [.. bases.Select(row => new BaseRow(row.MovementBaseID, row.Name, row.Classification, row.Description))],
                [.. equipment.Select(row => new PartRow(row.EquipmentID, row.Name))],
                [.. modifiers.Select(row => new PartRow(row.MovementModifierID, row.Name))],
                [.. movements.Select(movement => new HeldMovement(
                    movement.MovementDataID, NameOf(movement), movement.MovementBaseID, movement.EquipmentID,
                    movement.MovementModifierID, movement.Setup, movement.CreatedAt,
                    uses.GetValueOrDefault(movement.MovementDataID, MovementUses.None)))]),
            bases, equipment, modifiers, movements, sessions, routines, pins, notes,
            new LibraryCounts(movements.Count, bases.Count, equipment.Count, modifiers.Count));
    }

    private static MovementName NameOf(MovementData movement) =>
        new(movement.MovementBase.Name, movement.Equipment.Name, movement.MovementModifier?.Name ?? string.Empty);

    // ---- Stage ----

    private Task<Result<Planned, Problem>> StageAsync(Planned planned, CancellationToken ct) =>
        RepointMerged(planned)
            .MapAsync(async _ =>
            {
                planned.Held.SetAside(planned.Plan.Parts.Select(outcome => outcome.GoingRowId).OfType<Guid>());
                RenameAndCreateParts(planned);
                CarryWhatGoingBasesSaid(planned);
                RemoveMergedAndRetired(planned);
                await _context.SaveChangesAsync(ct);

                SettleSurvivors(planned);
                AddSeeded(planned);
                RemoveGoingParts(planned);
                await _context.SaveChangesAsync(ct);
                return planned;
            });

    /// <summary>
    /// Moves every set, prescription, routine entry, pin and injury note on a merged movement to the
    /// one it merges into, and hands the kept movement a setup it lacked. Records follow by replay.
    /// </summary>
    private Result<Planned, Problem> RepointMerged(Planned planned)
    {
        var merges = planned.Plan.Merges;
        var held = planned.Held;

        return held.Routines
            .Where(routine => routine.Movements.Any(movement => merges.ContainsKey(movement.MovementDataId)))
            .Traverse(routine => MovementMerge.Repointed(routine, merges).Map(content => (Routine: routine, Content: content)))
            .Map(revisions =>
            {
                foreach (var (routine, content) in revisions) routine.Revise(content);

                // Remove-then-add, as every session rewrite is: a session is an immutable record.
                foreach (var session in held.Sessions.Where(session => MovementMerge.Names(session, merges)))
                {
                    _context.Sessions.Remove(session);
                    _context.Sessions.Add(MovementMerge.Repointed(session, merges));
                }

                foreach (var pin in held.Pins.Where(pin => merges.ContainsKey(pin.MovementDataId)))
                    pin.FollowMerge(merges[pin.MovementDataId]);

                foreach (var note in held.InjuryNotes.Where(note => note.MovementIDs.Any(merges.ContainsKey)))
                    note.MovementIDs = MovementMerge.Repointed(note, merges);

                var movements = held.Movements.ToDictionary(movement => movement.MovementDataID);
                foreach (var carry in planned.Plan.SetupCarries)
                    movements[carry.IntoId].ReplaceSetup(carry.Setup);

                return planned;
            });
    }

    private void RenameAndCreateParts(Planned planned)
    {
        var held = planned.Held;
        foreach (var outcome in planned.Plan.Parts)
        {
            switch (outcome.Fate)
            {
                case PartFate.Renamed renamed:
                    held.Rename(outcome.Part, renamed.RowId, renamed.To);
                    break;
                case PartFate.Created:
                    _context.Add(held.Create(outcome.Part, outcome.Name));
                    break;
                case PartFate.Folded or PartFate.Removed:
                    break;
                default:
                    throw new System.Diagnostics.UnreachableException();
            }
        }
    }

    private static void CarryWhatGoingBasesSaid(Planned planned)
    {
        foreach (var carry in planned.Plan.BaseCarries)
        {
            var receiver = planned.Held.BaseNamed(carry.Into);
            if (carry.Statement is StatedClassification.Stated stated) receiver.State(stated.Classification);
            if (carry.Description.Length > 0) receiver.Description = carry.Description;
        }
    }

    private void RemoveMergedAndRetired(Planned planned)
    {
        var going = planned.Plan.Movements
            .Where(movement => movement.Fate is MovementFate.Merged or MovementFate.Retired)
            .Select(movement => movement.MovementDataId)
            .ToHashSet();

        _context.MovementDatas.RemoveRange(planned.Held.Movements.Where(movement => going.Contains(movement.MovementDataID)));
    }

    /// <summary>Points each kept movement at the rows that spell its name now.</summary>
    private static void SettleSurvivors(Planned planned)
    {
        var movements = planned.Held.Movements.ToDictionary(movement => movement.MovementDataID);
        foreach (var (id, name) in planned.Plan.Survivors)
        {
            var (movementBase, equipment, modifier) = planned.Held.PartsFor(name);
            var movement = movements[id];
            movement.MovementBaseID = movementBase.MovementBaseID;
            movement.MovementBase = movementBase;
            movement.EquipmentID = equipment.EquipmentID;
            movement.Equipment = equipment;
            movement.MovementModifierID = modifier?.MovementModifierID;
            movement.MovementModifier = modifier;
        }
    }

    private void AddSeeded(Planned planned)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        foreach (var name in planned.Plan.Added)
        {
            var (movementBase, equipment, modifier) = planned.Held.PartsFor(name);
            _context.MovementDatas.Add(new MovementData
            {
                MovementDataID = Guid.NewGuid(),
                UserID = planned.UserId,
                MovementBaseID = movementBase.MovementBaseID,
                MovementBase = movementBase,
                EquipmentID = equipment.EquipmentID,
                Equipment = equipment,
                MovementModifierID = modifier?.MovementModifierID,
                MovementModifier = modifier,
                CreatedAt = now,
            });
        }
    }

    private void RemoveGoingParts(Planned planned)
    {
        foreach (var outcome in planned.Plan.Parts.Where(outcome => outcome.GoingRowId is not null))
            _context.Remove(planned.Held.Row(outcome.Part, outcome.GoingRowId!.Value));
    }

    // ---- Replay ----

    private Task<Result<Replayed, Problem>> ReplayAsync(Guid userId, Planned planned, CancellationToken ct) =>
        planned.Plan.Merges.Count == 0
            ? Task.FromResult(Result<Replayed, Problem>.Success(new Replayed(planned, new RecordReplay.NotNeeded())))
            : _records.ReplayAsync(userId, ct)
                .MapAsync(report => new Replayed(planned, new RecordReplay.Replayed(report)));

    // ---- Report ----

    private async Task<TidyReport> ReportAsync(Guid userId, Replayed replayed, TidyRun run, CancellationToken ct)
    {
        var bases = await _context.MovementBases.AsNoTracking().Where(row => row.UserID == userId).ToListAsync(ct);
        var after = new LibraryCounts(
            await _context.MovementDatas.CountAsync(movement => movement.UserID == userId, ct),
            bases.Count,
            await _context.Equipments.CountAsync(row => row.UserID == userId, ct),
            await _context.MovementModifiers.CountAsync(row => row.UserID == userId, ct));

        return new TidyReport(
            replayed.Planned.Held.Before,
            after,
            replayed.Planned.Plan,
            replayed.Records,
            [.. bases.Where(row => _patterns.StandingOf(row) is ClassificationStanding.Unclassified).Select(row => row.Name).Order()],
            run);
    }

    // ---- The values carried between steps ----

    private sealed record Planned(HeldLibrary Held, TidyPlan Plan)
    {
        public Guid UserId => Held.UserId;
    }

    private sealed record Replayed(Planned Planned, RecordReplay Records);
}
