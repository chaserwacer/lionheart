using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Import;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Strong;

/// <summary>
/// Imports a Strong export as completed training sessions, with the movements they name and the
/// personal records they set.
/// </summary>
/// <remarks>
/// The athlete logged in Strong before Lionheart could take a set, and wants that history where
/// the History tab, the records and the chat model can see it.
/// <para>
/// <b>Re-runnable.</b> Each workout imports under an id derived from its own key, so a workout
/// already held is recognised and left alone, and running the same export twice adds nothing.
/// </para>
/// <para>
/// <b>Records as if logged live.</b> The new sessions go through the same detector a completed
/// session goes through, all in one batch, which it processes oldest first — so the records come
/// out exactly as they would have, had each workout been completed here on its day. That only
/// holds while nothing already standing postdates the history being added: such a record would
/// be compared against workouts older than itself, and its chain would end up claiming it beat
/// sessions that came before it. The import refuses in that case, naming each record, rather than
/// write a chain that misreports the order things happened in.
/// </para>
/// <para>
/// <b>All or nothing.</b> Movements and sessions are written in one transaction. The movements
/// are saved a step ahead of the sessions only because the detector reads the athlete's movements
/// back from the database to decide which sets may raise records.
/// </para>
/// </remarks>
public sealed class StrongImporter
{
    private readonly ModelContext _context;
    private readonly IPersonalRecordDetector _records;
    private readonly MovementMap _map;
    private readonly TimeZoneInfo _zone;
    private readonly TimeProvider _clock;

    /// <param name="zone">Where the export's wall-clock times were kept; Strong records no offset.</param>
    public StrongImporter(
        ModelContext context,
        IPersonalRecordDetector records,
        MovementMap map,
        TimeZoneInfo zone,
        TimeProvider clock)
    {
        _context = context;
        _records = records;
        _map = map;
        _zone = zone;
        _clock = clock;
    }

    public Task<Result<StrongImportReport, Problem>> ImportAsync(
        string exportPath, Guid userId, CancellationToken ct) =>
        _context.KnownAthleteAsync(userId, ct)
            .BindAsync(_ => StrongExportReader.Read(exportPath, _zone))
            .BindAsync(Mapped)
            .MapAsync(export => UnimportedAsync(userId, export, ct))
            .MapAsync(pending => StageAsync(userId, pending, ct))
            .BindAsync(staged => InOrderAsync(userId, staged, ct))
            .MapAsync(staged => CommitAsync(staged, ct));

    // ---- Guard ----

    private Result<MappedExport, Problem> Mapped(IReadOnlyList<StrongWorkout> workouts) =>
        _map.Cover(workouts.SelectMany(workout => workout.Sets).Select(set => set.Exercise))
            .Map(mappings => new MappedExport(workouts, mappings));

    // ---- Stage ----

    private async Task<PendingImport> UnimportedAsync(Guid userId, MappedExport export, CancellationToken ct)
    {
        var ids = export.Workouts.Select(workout => workout.Key.SessionIdFor(userId)).ToList();
        var held = (await _context.Sessions
                .Where(session => ids.Contains(session.TrainingSessionId))
                .Select(session => session.TrainingSessionId)
                .ToListAsync(ct))
            .ToHashSet();

        var unimported = export.Workouts
            .Where(workout => !held.Contains(workout.Key.SessionIdFor(userId)))
            .ToList();

        return new PendingImport(export, unimported, export.Workouts.Count - unimported.Count);
    }

    /// <summary>
    /// The sessions the new workouts become, with whatever movements they need staged beside them.
    /// </summary>
    /// <remarks>
    /// Only the names the new workouts use reach the catalogue, so a re-run creates nothing for
    /// workouts it is not importing.
    /// </remarks>
    private async Task<StagedImport> StageAsync(Guid userId, PendingImport pending, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var catalogue = await MovementCatalogue.LoadAsync(_context, userId, now, ct);

        var applied = pending.Workouts
            .SelectMany(workout => workout.Sets)
            .Select(set => set.Exercise)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(exercise => pending.Export.Mappings[exercise])
            .ToList();

        var movementIds = applied.ToDictionary(
            mapping => mapping.Name,
            mapping => catalogue.IdOf(mapping.Movement),
            StringComparer.OrdinalIgnoreCase);

        return new StagedImport(
            pending,
            catalogue,
            [.. pending.Workouts.Select(workout => StrongSessions.ToSession(workout, userId, movementIds, now))],
            [.. applied.Where(mapping => mapping.IsUnsure).OrderBy(mapping => mapping.Name, StringComparer.OrdinalIgnoreCase)]);
    }

    /// <summary>
    /// Refuses when a record already standing was set after a session this import would add on the
    /// same record, since the replay could then no longer run in the order things happened.
    /// </summary>
    /// <remarks>
    /// Only the identities the new sessions actually contest are checked: a standing record on a
    /// movement, unit or kind this history never touches cannot be reordered by it.
    /// </remarks>
    private async Task<Result<StagedImport, Problem>> InOrderAsync(
        Guid userId, StagedImport staged, CancellationToken ct)
    {
        var firstContested = staged.Sessions
            .SelectMany(session => RecordCandidates.From(session)
                .Select(candidate => (candidate.Key, session.Period.StartTimeUtc)))
            .GroupBy(claim => claim.Key, claim => claim.StartTimeUtc)
            .ToDictionary(claims => claims.Key, claims => claims.Min());

        if (firstContested.Count == 0) return Result<StagedImport, Problem>.Success(staged);

        var standing = await _context.PersonalRecords
            .AsNoTracking()
            .Where(record => record.UserId == userId
                             && EF.Property<bool>(record, RemodelConfiguration.RecordIsStandingColumn))
            .ToListAsync(ct);

        var later = standing
            .Where(record => firstContested.TryGetValue(record.Key, out var first) && record.AchievedAt >= first)
            .OrderBy(record => record.AchievedAt)
            .ToList();

        return later.Count == 0
            ? Result<StagedImport, Problem>.Success(staged)
            : Result<StagedImport, Problem>.Failure(new Problem.Conflict(
                $"{later.Count} standing record(s) were set after workouts this import would add, so the history "
                + "cannot be replayed oldest first: each would be compared against workouts older than itself, "
                + "and the chains would claim records beat sessions that came before them. Nothing has been written. "
                + "Withdrawing one puts back the record it beat, which may be just as late, so run again until "
                + "this list is empty:\n"
                + string.Join("\n", later.Select(record => $"  {Describe(record, staged.Catalogue)}"))));
    }

    private static string Describe(PersonalRecord record, MovementCatalogue catalogue)
    {
        var subject = record.Subject switch
        {
            RecordSubject.Movement movement => catalogue.NameOf(movement.MovementDataId),
            _ => "trail running",
        };
        var bracket = record.Kind.Bracket > 0 ? $" {record.Kind.Bracket}" : string.Empty;
        var unit = record.Achievement.HeldIn is { } held ? $" in {held}" : string.Empty;

        return $"{subject}: {record.Kind.Name}{bracket}{unit}, set {record.AchievedAt:yyyy-MM-dd}";
    }

    // ---- Commit ----

    private async Task<StrongImportReport> CommitAsync(StagedImport staged, CancellationToken ct)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        await _context.SaveChangesAsync(ct);
        await _records.StageAsync(staged.Sessions, ct);
        _context.Sessions.AddRange(staged.Sessions);

        var established = _context.ChangeTracker.Entries<PersonalRecord>()
            .Count(entry => entry.State == EntityState.Added);

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new StrongImportReport(
            staged.Pending.Export.Workouts.Count,
            staged.Pending.AlreadyImported,
            staged.Sessions.Count,
            StrongSetTally.Of(staged.Pending.Workouts),
            staged.Catalogue.Growth,
            established,
            staged.UnsureMappings);
    }

    // ---- The values carried between steps ----

    private sealed record MappedExport(
        IReadOnlyList<StrongWorkout> Workouts, IReadOnlyDictionary<string, MovementMapping> Mappings);

    private sealed record PendingImport(
        MappedExport Export, IReadOnlyList<StrongWorkout> Workouts, int AlreadyImported);

    private sealed record StagedImport(
        PendingImport Pending,
        MovementCatalogue Catalogue,
        IReadOnlyList<CompletedTrainingSession> Sessions,
        IReadOnlyList<MovementMapping> UnsureMappings);
}
