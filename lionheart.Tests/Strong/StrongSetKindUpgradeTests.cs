using lionheart.Functional;
using lionheart.Model.Training;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Strong;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;
using static lionheart.Tests.Strong.StrongExports;

namespace lionheart.Tests.Strong;

/// <summary>
/// The set-kind upgrade against a real database: which held sessions it rewrites, which it leaves
/// alone, that it rewrites only sets and notes, and that records and the catalogue do not move.
/// </summary>
/// <remarks>
/// Each test imports a workout with the current importer and then rewrites the stored session into
/// the shape the earlier importer wrote, stated by hand here rather than produced by the code under
/// test.
/// </remarks>
public class StrongSetKindUpgradeTests : IDisposable
{
    private static readonly DateOnly SquatDay = new(2025, 1, 10);

    private readonly RemodelDatabase _database = new();

    public void Dispose() => _database.Dispose();

    /// <summary>One workout holding every kind of set Strong writes.</summary>
    private static string[] SquatWorkout =>
    [
        Set("2025-01-10 17:00:00", "Squat (Barbell)", "W", 135, 10),
        Set("2025-01-10 17:00:00", "Squat (Barbell)", "1", 225, 5, rpe: "8"),
        Set("2025-01-10 17:00:00", "Squat (Barbell)", "F", 205, 6),
        Set("2025-01-10 17:00:00", "Squat (Barbell)", "D", 185, 8),
    ];

    private static StrongMovementMap SquatMap => Map(("Squat (Barbell)", "Squat", "Barbell", ""));

    private static CompletedTrainingSession AsTheEarlierImportWroteIt(CompletedTrainingSession session, Guid squat) =>
        session with
        {
            Sets =
            [
                new FreeformSet { Source = new Texted("Warm-up — Squat (Barbell): 135 lb × 10", 0), Facets = [] },
                EarlierLift(squat, 5, 225, Rpe.Of(8)),
                EarlierLift(squat, 6, 205, Rpe.Unrated),
                EarlierLift(squat, 8, 185, Rpe.Unrated),
            ],
            Notes = "Drop sets: Squat (Barbell) 185 lb × 8\nTaken to failure: Squat (Barbell) 205 lb × 6",
        };

    private static LiftSet EarlierLift(Guid movement, int reps, double weight, Rpe rpe) =>
        new(movement, reps, weight, WeightUnit.Pounds, rpe, SetKind.Working) { Source = new Formed(), Facets = [] };

    [Fact]
    public async Task A_session_the_earlier_import_wrote_is_upgraded_to_set_kinds()
    {
        using var export = await ImportedAsEarlierAsync(session => AsTheEarlierImportWroteIt(session, SquatOf(session)));

        var report = ResultAssert.Succeeded(await UpgradeAsync(export.Path));

        Assert.Equal((1, 0, 0), (report.Upgraded, report.AlreadyCurrent, report.LeftAlone.Count));
        Assert.Equal(new StrongSetTally(Working: 1, WarmUps: 1, Drops: 1, Failures: 1, RestTimers: 0), report.UpgradedSets);

        var upgraded = await SessionOnAsync(SquatDay);
        Assert.Equal(
            [(SetKind.WarmUp, 135.0, 10), (SetKind.Working, 225, 5), (SetKind.Failure, 205, 6), (SetKind.Drop, 185, 8)],
            upgraded.Sets.Select(set => Assert.IsType<LiftSet>(set)).Select(lift => (lift.Kind, lift.Weight, lift.Reps)));
        Assert.Equal(string.Empty, upgraded.Notes);
    }

    [Fact]
    public async Task An_upgrade_changes_no_record_session_id_or_movement()
    {
        using var export = await ImportedAsEarlierAsync(session => AsTheEarlierImportWroteIt(session, SquatOf(session)));
        var before = Held();

        ResultAssert.Succeeded(await UpgradeAsync(export.Path));

        Assert.Equal(before, Held());
    }

    [Fact]
    public async Task An_upgrade_replaces_only_the_sets_and_notes()
    {
        // A routine the athlete saved the session as since is theirs, not the import's.
        var routine = Guid.NewGuid();
        using var export = await ImportedAsEarlierAsync(session =>
            AsTheEarlierImportWroteIt(session, SquatOf(session)) with { Origin = new RoutineOrigin.FromRoutine(routine) });
        var before = await SessionOnAsync(SquatDay);

        ResultAssert.Succeeded(await UpgradeAsync(export.Path));

        var after = await SessionOnAsync(SquatDay);
        Assert.Equal(new RoutineOrigin.FromRoutine(routine), after.Origin);
        Assert.Equal(
            (before.TrainingSessionId, before.Period, before.CreatedAt, before.Provenance),
            (after.TrainingSessionId, after.Period, after.CreatedAt, after.Provenance));
    }

    [Fact]
    public async Task A_session_whose_notes_were_edited_since_import_is_left_alone()
    {
        using var export = await ImportedAsEarlierAsync(session =>
        {
            var earlier = AsTheEarlierImportWroteIt(session, SquatOf(session));
            return earlier with { Notes = earlier.Notes + "\nknees caved on the last set" };
        });

        var report = ResultAssert.Succeeded(await UpgradeAsync(export.Path));

        Assert.Equal(new DateTime(2025, 1, 10, 17, 0, 0), Assert.Single(report.LeftAlone).Start);
        Assert.Equal(0, report.Upgraded);
        var untouched = await SessionOnAsync(SquatDay);
        Assert.IsType<FreeformSet>(untouched.Sets[0]);
        Assert.EndsWith("knees caved on the last set", untouched.Notes);
    }

    [Fact]
    public async Task A_session_written_under_a_mapping_that_has_changed_is_left_alone()
    {
        // The stored lifts name a movement other than the one the map sends this exercise to.
        var elsewhere = Guid.NewGuid();
        using var export = await ImportedAsEarlierAsync(session => AsTheEarlierImportWroteIt(session, elsewhere));

        var report = ResultAssert.Succeeded(await UpgradeAsync(export.Path));

        Assert.Single(report.LeftAlone);
        Assert.All((await SessionOnAsync(SquatDay)).Sets.OfType<LiftSet>(), lift => Assert.Equal(elsewhere, lift.MovementDataId));
    }

    [Fact]
    public async Task A_session_already_in_the_current_shape_is_counted_current_and_left_as_it_is()
    {
        using var export = new ExportFile(SquatWorkout);
        await ImportAsync(export.Path);
        var before = await SessionOnAsync(SquatDay);

        var report = ResultAssert.Succeeded(await UpgradeAsync(export.Path));

        Assert.Equal((0, 1, 0), (report.Upgraded, report.AlreadyCurrent, report.LeftAlone.Count));
        Assert.Equal(before.CreatedAt, (await SessionOnAsync(SquatDay)).CreatedAt);
    }

    [Fact]
    public async Task A_workout_the_database_does_not_hold_is_counted_and_not_imported()
    {
        using var export = new ExportFile(SquatWorkout);

        var report = ResultAssert.Succeeded(await UpgradeAsync(export.Path));

        Assert.Equal((1, 1, 0), (report.WorkoutsInExport, report.NotImported, report.Upgraded));
        Assert.Equal("0:|0:|0:", Held());
    }

    [Fact]
    public async Task A_movement_named_with_other_capitalisation_still_matches()
    {
        // The catalogue matches names ignoring case and spaces; the upgrade's own read must too.
        using var export = await ImportedAsEarlierAsync(session => AsTheEarlierImportWroteIt(session, SquatOf(session)));
        _database.ExecuteNonQuery("UPDATE MovementBases SET Name = ' SQUAT '");

        Assert.Equal(1, ResultAssert.Succeeded(await UpgradeAsync(export.Path)).Upgraded);
    }

    [Fact]
    public async Task An_upgrade_for_an_athlete_who_does_not_exist_is_refused()
    {
        using var export = new ExportFile(SquatWorkout);
        await using var context = _database.CreateContext();

        var refused = await Upgrader(context).UpgradeAsync(export.Path, Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(refused));
    }

    // ---- Helpers ----

    private async Task<ExportFile> ImportedAsEarlierAsync(Func<CompletedTrainingSession, CompletedTrainingSession> rewrite)
    {
        var export = new ExportFile(SquatWorkout);
        await ImportAsync(export.Path);
        await RewriteAsync(SquatDay, rewrite);
        return export;
    }

    private async Task ImportAsync(string path)
    {
        await using var context = _database.CreateContext();
        ResultAssert.Succeeded(await Importer(context, SquatMap).ImportAsync(path, _database.UserId, CancellationToken.None));
    }

    private async Task<Result<StrongUpgradeReport, Problem>> UpgradeAsync(string path)
    {
        await using var context = _database.CreateContext();
        return await Upgrader(context).UpgradeAsync(path, _database.UserId, CancellationToken.None);
    }

    private static StrongSetKindUpgrader Upgrader(Data.ModelContext context) =>
        new(context, SquatMap, NewYork, TimeProvider.System);

    private async Task<CompletedTrainingSession> SessionOnAsync(DateOnly day)
    {
        await using var context = _database.CreateContext();
        var sessions = await context.Sessions.OfType<CompletedTrainingSession>().AsNoTracking().ToListAsync();
        return sessions.Single(session => session.Date == day);
    }

    private static Guid SquatOf(CompletedTrainingSession session) =>
        session.Sets.OfType<LiftSet>().Select(lift => lift.MovementDataId).Distinct().Single();

    /// <summary>Replaces the stored session for a day, as a row no route can produce.</summary>
    private async Task RewriteAsync(DateOnly day, Func<CompletedTrainingSession, CompletedTrainingSession> rewrite)
    {
        await using var context = _database.CreateContext();
        var stored = (await context.Sessions.OfType<CompletedTrainingSession>().ToListAsync()).Single(session => session.Date == day);
        context.Sessions.Remove(stored);
        context.Sessions.Add(rewrite(stored));
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Every record row whole, the session identities and the movement ids, as stored — so
    /// "unchanged" means byte for byte, not equal after a round trip.
    /// </summary>
    private string Held() => string.Join("|",
        Rows("SELECT PersonalRecordId || Achievement || AchievedAt || Standing || Lineage AS row FROM PersonalRecords"),
        Rows("SELECT TrainingSessionId || CreatedAt || Period AS row FROM Sessions"),
        Rows("SELECT MovementDataID AS row FROM MovementDatas"));

    private string Rows(string select) =>
        (string)_database.ExecuteScalar(
            $"SELECT count(*) || ':' || coalesce(group_concat(row, ';'), '') FROM ({select} ORDER BY 1)")!;
}
