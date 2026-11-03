using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.Programs;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Import;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.CoachedSheets;

/// <summary>
/// Imports the athlete's coached-programme spreadsheets, as training-os derived them, as completed
/// training sessions in their programmes, and rebuilds his records over the whole history.
/// </summary>
/// <remarks>
/// The sheets are the 2023–24 powerlifting blocks, the period of his lifetime bests, and older
/// than everything the Strong import brought in.
/// <para>
/// <b>Performed sets only.</b> A row is imported when the athlete entered its weight. Rows whose
/// weight the spreadsheet computed, and the few he marked as not done or that are plainly typing
/// slips, are listed with their evidence in <c>coached-sheets.json</c>, set aside, and named in
/// the session's notes instead.
/// </para>
/// <para>
/// <b>Records rebuilt.</b> These sessions predate every record standing, so detecting their records
/// incrementally — the way the Strong import does, and refuses to when history is older — would
/// write chains claiming 2025 records beat 2023 sets. The import instead replays every completed
/// session through the detector, oldest first (<see cref="PersonalRecordRebuild"/>).
/// </para>
/// <para>
/// <b>Re-runnable and all or nothing.</b> Session and programme ids derive from the log, so a
/// second run recognises what it holds and adds nothing; movements, programmes, sessions and the
/// rebuilt records commit in one transaction.
/// </para>
/// </remarks>
public sealed class CoachedSheetImporter
{
    private readonly ModelContext _context;
    private readonly PersonalRecordRebuild _records;
    private readonly MovementMap _movements;
    private readonly SheetDecisions _decisions;
    private readonly TimeZoneInfo _zone;
    private readonly TimeProvider _clock;

    /// <param name="zone">Where the athlete was training; the sessions are placed at noon on their date there.</param>
    public CoachedSheetImporter(
        ModelContext context,
        PersonalRecordRebuild records,
        MovementMap movements,
        SheetDecisions decisions,
        TimeZoneInfo zone,
        TimeProvider clock)
    {
        _context = context;
        _records = records;
        _movements = movements;
        _decisions = decisions;
        _zone = zone;
        _clock = clock;
    }

    public Task<Result<CoachedSheetImportReport, Problem>> ImportAsync(string logPath, Guid userId, CancellationToken ct) =>
        _context.KnownAthleteAsync(userId, ct)
            .BindAsync(_ => _records.ReplayableAsync(userId, ct))
            .BindAsync(_ => SheetLogReader.Read(logPath))
            .BindAsync(log => SheetHistory.Of(log, _decisions))
            .BindAsync(Mapped)
            .MapAsync(history => UnimportedAsync(userId, history, ct))
            .MapAsync(pending => StageAsync(userId, pending, ct))
            .BindAsync(staged => CommitAsync(userId, staged, ct));

    // ---- Guard ----

    /// <summary>Every lift's movement and every sheet's programme, or each one the files do not cover.</summary>
    private Result<MappedHistory, Problem> Mapped(SheetHistory history)
    {
        var unprogrammed = history.Workouts
            .Select(workout => workout.Source.Sheet)
            .Distinct()
            .Where(sheet => !_decisions.Programs.ContainsKey(sheet))
            .Order(StringComparer.Ordinal)
            .Select(sheet => new InvalidField(sheet,
                $"Sheet '{sheet}' has no programme in {SheetDecisions.FileName}, so its sessions would belong to nothing."))
            .ToList();

        return unprogrammed.Count > 0
            ? Result<MappedHistory, Problem>.Failure(new Problem.Invalid(unprogrammed))
            : _movements.Cover(history.Performed.Select(set => set.Exercise))
                .Map(mappings => new MappedHistory(history, mappings));
    }

    // ---- Stage ----

    private async Task<PendingImport> UnimportedAsync(Guid userId, MappedHistory mapped, CancellationToken ct)
    {
        var workouts = mapped.History.Workouts;
        var sessionIds = workouts.Select(workout => SheetSessions.SessionIdFor(workout, userId)).ToList();
        var programIds = workouts.Select(workout => SheetSessions.ProgramIdFor(workout.Source.Sheet, userId)).Distinct().ToList();

        var heldSessions = (await _context.Sessions
                .Where(session => sessionIds.Contains(session.TrainingSessionId))
                .Select(session => session.TrainingSessionId)
                .ToListAsync(ct))
            .ToHashSet();

        var heldPrograms = (await _context.Programs
                .Where(program => programIds.Contains(program.TrainingProgramId))
                .Select(program => program.TrainingProgramId)
                .ToListAsync(ct))
            .ToHashSet();

        var unimported = workouts
            .Where(workout => !heldSessions.Contains(SheetSessions.SessionIdFor(workout, userId)))
            .ToList();

        return new PendingImport(mapped, unimported, workouts.Count - unimported.Count, heldPrograms);
    }

    /// <summary>
    /// The sessions the new workouts become, with the movements and programmes they need staged
    /// beside them.
    /// </summary>
    /// <remarks>
    /// Only what the new workouts use is staged, so a re-run creates nothing. A programme is
    /// created when a new session needs one the athlete does not hold, spanning every session the
    /// log has for its sheet.
    /// </remarks>
    private async Task<StagedImport> StageAsync(Guid userId, PendingImport pending, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var catalogue = await MovementCatalogue.LoadAsync(_context, userId, now, ct);
        var templateText = _decisions.TemplateText.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var applied = pending.Workouts
            .SelectMany(workout => workout.Sets)
            .Select(set => set.Exercise)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(exercise => pending.Mapped.Mappings[exercise])
            .ToList();

        var movementIds = applied.ToDictionary(
            mapping => mapping.Name,
            mapping => catalogue.IdOf(mapping.Movement),
            StringComparer.OrdinalIgnoreCase);

        var programs = pending.Workouts
            .Select(workout => workout.Source.Sheet)
            .Distinct()
            .Where(sheet => !pending.HeldPrograms.Contains(SheetSessions.ProgramIdFor(sheet, userId)))
            .Select(sheet => SheetSessions.ToProgram(
                _decisions.Programs[sheet],
                [.. pending.Mapped.History.Workouts.Where(workout => workout.Source.Sheet == sheet)],
                userId))
            .ToList();

        var sessions = pending.Workouts
            .Select(workout => SheetSessions.ToSession(
                workout, _decisions.Programs[workout.Source.Sheet], templateText, userId, movementIds, _zone, now))
            .ToList();

        return new StagedImport(
            pending,
            catalogue,
            programs,
            sessions,
            [.. applied.Where(mapping => mapping.IsUnsure).OrderBy(mapping => mapping.Name, StringComparer.Ordinal)]);
    }

    // ---- Commit ----

    /// <remarks>
    /// The rebuild saves inside the import's transaction, so everything lands together; a rebuild
    /// that refuses leaves nothing of the import written. A run that adds no session leaves the
    /// records alone.
    /// </remarks>
    private Task<Result<CoachedSheetImportReport, Problem>> CommitAsync(
        Guid userId, StagedImport staged, CancellationToken ct) =>
        _context.InTransactionAsync(() => WriteAsync(userId, staged, ct), ct);

    private async Task<Result<CoachedSheetImportReport, Problem>> WriteAsync(
        Guid userId, StagedImport staged, CancellationToken ct)
    {
        _context.Programs.AddRange(staged.Programs);
        _context.Sessions.AddRange(staged.Sessions);
        await _context.SaveChangesAsync(ct);

        var records = staged.Sessions.Count == 0
            ? Result<RecordsOutcome, Problem>.Success(new RecordsOutcome.Untouched())
            : await _records.ReplayAsync(userId, ct).MapAsync(RecordsOutcome (report) => new RecordsOutcome.Rebuilt(report));

        return records.Map(outcome => Report(staged, outcome));
    }

    private static CoachedSheetImportReport Report(StagedImport staged, RecordsOutcome records)
    {
        var history = staged.Pending.Mapped.History;

        return new CoachedSheetImportReport(
            SheetSetsInLog: history.Log.Sets.Count,
            StrongRowsSetAside: history.Log.StrongRowsSetAside,
            NotPerformed:
            [
                .. history.NotPerformed
                    .GroupBy(row => row.Kind)
                    .OrderBy(rows => rows.Key)
                    .Select(rows => new NotPerformedTally(rows.Key, rows.Count(), rows.Sum(row => row.Sets)))
            ],
            ClustersSplit: history.ClustersSplit,
            SessionsWithNothingPerformed: history.SessionsWithNothingPerformed,
            SessionsInLog: history.Workouts.Count,
            AlreadyImported: staged.Pending.AlreadyImported,
            SessionsCreated: staged.Sessions.Count,
            SetsCreated: staged.Sessions.Sum(session => session.Sets.Count),
            ProgramsCreated: [.. staged.Programs.Select(program => program.Title)],
            Catalogue: staged.Catalogue.Growth,
            UnsureMappings: staged.UnsureMappings,
            Records: records);
    }

    // ---- The values carried between steps ----

    private sealed record MappedHistory(SheetHistory History, IReadOnlyDictionary<string, MovementMapping> Mappings);

    private sealed record PendingImport(
        MappedHistory Mapped, IReadOnlyList<SheetWorkout> Workouts, int AlreadyImported, IReadOnlySet<Guid> HeldPrograms);

    private sealed record StagedImport(
        PendingImport Pending,
        MovementCatalogue Catalogue,
        IReadOnlyList<CompletedTrainingProgram> Programs,
        IReadOnlyList<CompletedTrainingSession> Sessions,
        IReadOnlyList<MovementMapping> UnsureMappings);
}
