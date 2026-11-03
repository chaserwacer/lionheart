using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.Sessions;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Strong;

/// <summary>What one run of the set-kind upgrade did to the sessions the import already holds.</summary>
/// <remarks>
/// <see cref="NotImported"/> counts workouts in the export the database holds no session for: the
/// upgrade only rewrites, so they are untouched and left to the import. <see cref="UpgradedSets"/>
/// counts the sets of the upgraded workouts only, which is where the warm-ups, drops and failures
/// that gained a kind are.
/// </remarks>
public sealed record StrongUpgradeReport(
    int WorkoutsInExport,
    int NotImported,
    int Upgraded,
    int AlreadyCurrent,
    IReadOnlyList<StrongWorkoutKey> LeftAlone,
    StrongSetTally UpgradedSets);

/// <summary>
/// Brings the sessions an earlier Strong import wrote, before sets carried a kind, up to the shape
/// the import writes now (<see cref="StrongUpgrade"/> says which sessions qualify).
/// </summary>
/// <remarks>
/// Its own command rather than part of the import: it rewrites sessions already held and creates
/// nothing — no session, no movement, no record — so it reads the athlete's movements without
/// touching the catalogue, and a workout the database does not hold is simply counted. Records are
/// not re-detected: a warm-up raised none before and raises none after, and drop and failure sets
/// were lifts both times. Every upgrade lands in one save, so a run writes all of them or none.
/// </remarks>
public sealed class StrongSetKindUpgrader
{
    private readonly ModelContext _context;
    private readonly StrongMovementMap _map;
    private readonly TimeZoneInfo _zone;
    private readonly TimeProvider _clock;

    /// <param name="zone">Where the export's wall-clock times were kept; Strong records no offset.</param>
    public StrongSetKindUpgrader(ModelContext context, StrongMovementMap map, TimeZoneInfo zone, TimeProvider clock)
    {
        _context = context;
        _map = map;
        _zone = zone;
        _clock = clock;
    }

    public Task<Result<StrongUpgradeReport, Problem>> UpgradeAsync(
        string exportPath, Guid userId, CancellationToken ct) =>
        AthleteAsync(userId, ct)
            .BindAsync(_ => StrongExportReader.Read(exportPath, _zone))
            .BindAsync(workouts => _map.Cover(workouts.SelectMany(workout => workout.Sets).Select(set => set.Exercise))
                .Map(mappings => new MappedExport(workouts, mappings)))
            .MapAsync(export => HeldAsync(userId, export, ct))
            .MapAsync(held => ClassifiedAsync(userId, held, ct))
            .MapAsync(classified => CommitAsync(classified, ct));

    // ---- Guard ----

    private async Task<Result<Guid, Problem>> AthleteAsync(Guid userId, CancellationToken ct) =>
        await _context.LionheartUsers.AnyAsync(user => user.UserID == userId, ct)
            ? Result<Guid, Problem>.Success(userId)
            : Result<Guid, Problem>.Failure(new Problem.NotFound($"No Lionheart athlete with id {userId}."));

    // ---- Stage ----

    /// <summary>Each workout the database holds a session for, with that session, tracked for the rewrite.</summary>
    private async Task<HeldExport> HeldAsync(Guid userId, MappedExport export, CancellationToken ct)
    {
        var ids = export.Workouts.Select(workout => workout.Key.SessionIdFor(userId)).ToList();
        var sessions = (await _context.Sessions
                .Where(session => ids.Contains(session.TrainingSessionId))
                .ToListAsync(ct))
            .ToDictionary(session => session.TrainingSessionId);

        return new HeldExport(
            export,
            [
                .. export.Workouts
                    .Where(workout => sessions.ContainsKey(workout.Key.SessionIdFor(userId)))
                    .Select(workout => new HeldWorkout(workout, sessions[workout.Key.SessionIdFor(userId)]))
            ]);
    }

    private async Task<ClassifiedExport> ClassifiedAsync(Guid userId, HeldExport held, CancellationToken ct)
    {
        var movementIds = await HeldMovementsAsync(userId, held, ct);
        var now = _clock.GetUtcNow().UtcDateTime;

        return new ClassifiedExport(
            held,
            [.. held.Workouts.Select(workout => StrongUpgrade.Classify(workout.Session, workout.Workout, movementIds, now))]);
    }

    /// <summary>
    /// The athlete's movement for each exercise the held workouts name, where they have it.
    /// </summary>
    /// <remarks>
    /// Read-only, and matched the way the import's catalogue matches — base, equipment and modifier
    /// by name, ignoring case and surrounding spaces. An exercise whose movement the athlete does
    /// not have is absent, and any workout naming it cannot be what the import wrote.
    /// </remarks>
    private async Task<IReadOnlyDictionary<string, Guid>> HeldMovementsAsync(
        Guid userId, HeldExport held, CancellationToken ct)
    {
        var movements = (await _context.MovementDatas
                .AsNoTracking()
                .Where(movement => movement.UserID == userId)
                .OrderBy(movement => movement.MovementDataID)
                .Select(movement => new
                {
                    movement.MovementDataID,
                    Base = movement.MovementBase.Name,
                    Equipment = movement.Equipment.Name,
                    Modifier = movement.MovementModifier == null ? string.Empty : movement.MovementModifier.Name,
                })
                .ToListAsync(ct))
            .Select(movement => (movement.MovementDataID, Name: new MovementName(movement.Base, movement.Equipment, movement.Modifier)))
            .ToList();

        return held.Workouts
            .SelectMany(workout => workout.Workout.Sets)
            .Select(set => held.Export.Mappings[set.Exercise])
            .DistinctBy(mapping => mapping.StrongName, StringComparer.OrdinalIgnoreCase)
            .Select(mapping => (mapping.StrongName, Match: movements.FirstOrDefault(movement => SameName(movement.Name, mapping.Movement))))
            .Where(found => found.Match.Name is not null)
            .ToDictionary(found => found.StrongName, found => found.Match.MovementDataID, StringComparer.OrdinalIgnoreCase);
    }

    private static bool SameName(MovementName held, MovementName mapped) =>
        Same(held.Base, mapped.Base) && Same(held.Equipment, mapped.Equipment) && Same(held.Modifier, mapped.Modifier);

    private static bool Same(string left, string right) =>
        left.Trim().Equals(right.Trim(), StringComparison.OrdinalIgnoreCase);

    // ---- Commit ----

    /// <summary>Remove-then-add, as every session rewrite is: the records are immutable. One save.</summary>
    private async Task<StrongUpgradeReport> CommitAsync(ClassifiedExport classified, CancellationToken ct)
    {
        var upgrades = classified.Held.OfType<HeldSession.WrittenTheEarlierWay>().ToList();
        foreach (var upgrade in upgrades)
        {
            _context.Sessions.Remove(upgrade.Stored);
            _context.Sessions.Add(upgrade.Upgraded);
        }

        await _context.SaveChangesAsync(ct);

        return new StrongUpgradeReport(
            classified.Export.Export.Workouts.Count,
            classified.Export.Export.Workouts.Count - classified.Export.Workouts.Count,
            upgrades.Count,
            classified.Held.OfType<HeldSession.Current>().Count(),
            [.. classified.Held.OfType<HeldSession.ChangedSinceImport>().Select(left => left.Workout).OrderBy(key => key.Start)],
            StrongSetTally.Of(upgrades.Select(upgrade => upgrade.Workout)));
    }

    // ---- The values carried between steps ----

    private sealed record MappedExport(
        IReadOnlyList<StrongWorkout> Workouts, IReadOnlyDictionary<string, MovementMapping> Mappings);

    private sealed record HeldWorkout(StrongWorkout Workout, TrainingSession Session);

    private sealed record HeldExport(MappedExport Export, IReadOnlyList<HeldWorkout> Workouts);

    private sealed record ClassifiedExport(HeldExport Export, IReadOnlyList<HeldSession> Held);
}
