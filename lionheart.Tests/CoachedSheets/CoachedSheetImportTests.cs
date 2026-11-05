using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Programs;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.CoachedSheets;
using lionheart.Services.Import;
using lionheart.Services.Training.PersonalRecords;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;
using static lionheart.Tests.CoachedSheets.SheetLogs;
using static lionheart.Tests.Support.TrainingHistory;

namespace lionheart.Tests.CoachedSheets;

/// <summary>
/// The coached-programme import against a real database: what a programme day becomes, which rows
/// it leaves out, that it stores the history once, and that the records over the whole history
/// come out as a live log would have made them although the sheets are older than everything held.
/// </summary>
public class CoachedSheetImportTests : IDisposable
{
    private readonly RemodelDatabase _database = new();

    public void Dispose() => _database.Dispose();

    // ---- What a programme day becomes ----

    [Fact]
    public async Task A_programme_day_becomes_one_completed_session_at_noon_in_its_programme()
    {
        using var log = new LogFile(
            Set("2023-12-18", "mk-2", 1, "Monday", "squat-barbell", "Squat", 335, 3, rpe: "7"),
            Set("2023-12-18", "mk-2", 1, "Monday", "bench-press-barbell", "Bench Press", 225, 3));

        var report = ResultAssert.Succeeded(await ImportAsync(log.Path));

        Assert.Equal((1, 2), (report.SessionsCreated, report.SetsCreated));
        var session = Assert.Single(await SessionsAsync());
        Assert.Equal(new DateTimeOffset(2023, 12, 18, 12, 0, 0, TimeSpan.FromHours(-5)), Assert.IsType<Instant>(session.Period).At);
        Assert.Equal(
            [(335.0, 3, WeightUnit.Pounds, Rpe.Of(7)), (225.0, 3, WeightUnit.Pounds, Rpe.Unrated)],
            session.Sets.Cast<LiftSet>().Select(set => (set.Weight, set.Reps, set.Unit, set.Rpe)));
        Assert.StartsWith("Strength block — MK.2, week 1, Monday.\n", session.Notes);

        var program = Assert.IsType<CompletedTrainingProgram>(Assert.Single(await ProgramsAsync()));
        Assert.Equal(program.TrainingProgramId, Assert.IsType<InProgram>(session.Membership).TrainingProgramId);
        Assert.Equal(("Strength block — MK.2", new DateOnly(2023, 12, 18), new DateOnly(2023, 12, 18)),
            (program.Title, program.StartDate, program.EndDate));
    }

    [Fact]
    public async Task Strong_rows_in_the_log_are_set_aside_for_the_Strong_import()
    {
        using var log = new LogFile(
            Set("2023-12-18", "mk-2", 1, "Monday", "squat-barbell", "Squat", 335, 3),
            StrongRow("2024-08-20"));

        var report = ResultAssert.Succeeded(await ImportAsync(log.Path));

        Assert.Equal((1, 1, 1), (report.SheetSetsInLog, report.StrongRowsSetAside, report.SessionsCreated));
    }

    [Fact]
    public async Task A_row_the_spreadsheet_computed_is_left_out_of_the_session_and_named_in_its_notes()
    {
        using var log = new LogFile(
            Set("2023-01-23", "your-program-rpe", 1, "DAY 1", "squat-barbell", "SQUAT", 365, 3),
            Set("2023-01-23", "your-program-rpe", 1, "DAY 1", "squat-barbell", "SQUAT", 296.4, 5),
            Set("2023-01-23", "your-program-rpe", 1, "DAY 1", "squat-barbell", "SQUAT", 296.4, 5));
        var decisions = Decisions(NotPerformed("sheet:your-program-rpe#w1", "2023-01-23", "SQUAT", 296.4, 5, sets: 2));

        var report = ResultAssert.Succeeded(await ImportAsync(log.Path, decisions: decisions));

        Assert.Equal(new NotPerformedTally(NotPerformedKind.Computed, Rows: 1, Sets: 2), Assert.Single(report.NotPerformed));
        var session = Assert.Single(await SessionsAsync());
        Assert.Equal(365, Assert.IsType<LiftSet>(Assert.Single(session.Sets)).Weight);
        Assert.Contains(
            "Not counted as lifted: SQUAT 2 × 296.4 lb × 5, the spreadsheet computed the weight and it was never entered.",
            session.Notes.Split('\n'));
    }

    [Fact]
    public async Task A_day_with_nothing_performed_becomes_no_session()
    {
        using var log = new LogFile(
            Set("2023-01-23", "your-program-rpe", 1, "DAY 1", "squat-barbell", "SQUAT", 365, 3),
            Set("2023-01-28", "your-program-rpe", 1, "DAY 4", "squat-barbell", "SQUAT", 277.5, 6));
        var decisions = Decisions(NotPerformed("sheet:your-program-rpe#w1", "2023-01-28", "SQUAT", 277.5, 6, sets: 1));

        var report = ResultAssert.Succeeded(await ImportAsync(log.Path, decisions: decisions));

        Assert.Equal((1, 1), (report.SessionsCreated, report.SessionsWithNothingPerformed));
    }

    [Fact]
    public async Task A_cluster_recorded_as_one_set_is_stored_as_its_singles()
    {
        // A 1+1 cluster is two singles with the bar racked between; as one set of two it would
        // hold a two-rep max nobody lifted.
        using var log = new LogFile(
            Set("2023-01-24", "your-program-rpe", 1, "DAY 2", "paused-deadlift", "PAUSED DEADLIFT", 385, 2,
                note: "prescribed RPE 6.0 | CLUSTER SET (1+1)."));

        var report = ResultAssert.Succeeded(await ImportAsync(log.Path));

        Assert.Equal(1, report.ClustersSplit);
        var session = Assert.Single(await SessionsAsync());
        Assert.Equal([(385.0, 1), (385.0, 1)], session.Sets.Cast<LiftSet>().Select(set => (set.Weight, set.Reps)));
        Assert.DoesNotContain(await _database.RecordsAsync(), record => record.Kind == new RecordKind.RepMax(2));
    }

    [Fact]
    public async Task A_programme_reads_a_shared_slug_its_own_way()
    {
        // training-os gives every deadlift one slug; the RPE block pulled conventional and MK.2
        // pulled sumo, so the same slug lands on two movements and two record chains.
        using var log = new LogFile(
            Set("2023-04-10", "your-program-rpe", 12, "DAY 1", "deadlift-barbell", "DEADLIFT", 405, 1),
            Set("2023-12-18", "mk-2", 1, "Monday", "deadlift-barbell", "Deadlift", 455, 3));
        var map = Map(
            ("deadlift-barbell", "Deadlift", "Barbell", ""),
            ("sumo-deadlift-barbell", "Sumo Deadlift", "Barbell", ""));

        ResultAssert.Succeeded(await ImportAsync(log.Path, map: map, decisions: DecisionsWithSumoMk2()));

        await using var context = _database.CreateContext();
        var names = await context.MovementDatas
            .Select(movement => new { movement.MovementDataID, movement.MovementBase.Name })
            .ToDictionaryAsync(movement => movement.MovementDataID, movement => movement.Name);
        var pulls = (await SessionsAsync())
            .SelectMany(session => session.Sets.Cast<LiftSet>())
            .Select(set => (names[set.MovementDataId], set.Weight))
            .Order();
        Assert.Equal([("Deadlift", 405.0), ("Sumo Deadlift", 455.0)], pulls);
    }

    // ---- Re-running ----

    [Fact]
    public async Task Importing_the_same_log_twice_adds_nothing_the_second_time()
    {
        using var log = new LogFile(
            Set("2023-12-18", "mk-2", 1, "Monday", "squat-barbell", "Squat", 335, 3),
            Set("2023-12-21", "mk-2", 1, "Thursday", "bench-press-barbell", "Bench Press", 225, 3));
        await ImportAsync(log.Path);
        var first = await HeldAsync();

        var second = ResultAssert.Succeeded(await ImportAsync(log.Path));

        Assert.Equal((2, 0, 0), (second.AlreadyImported, second.SessionsCreated, second.ProgramsCreated.Count));
        Assert.Empty(second.Catalogue.Movements);
        Assert.IsType<RecordsOutcome.Untouched>(second.Records);
        Assert.Equal(first, await HeldAsync());
    }

    [Fact]
    public void A_session_imports_under_the_same_id_on_every_run_and_for_no_other_athlete()
    {
        var workout = new SheetWorkout(
            "2023-12-18-mk-2-monday", new DateOnly(2023, 12, 18), "Monday", new SheetSource("mk-2", 1), [], [], []);
        var athlete = Guid.NewGuid();

        Assert.Equal(SheetSessions.SessionIdFor(workout, athlete), SheetSessions.SessionIdFor(workout, athlete));
        Assert.NotEqual(SheetSessions.SessionIdFor(workout, athlete), SheetSessions.SessionIdFor(workout, Guid.NewGuid()));
    }

    // ---- Records over the whole history ----

    [Fact]
    public async Task Older_history_rebuilds_the_newer_records_into_the_chains_a_live_log_makes()
    {
        // Held already, from the Strong era: a 300 lb triple, then a 400 lb single.
        var squat = await _database.PlantMovementAsync("Squat", "Barbell");
        await _database.CompleteInTurnAsync(Strong(squat, _database.UserId));
        using var log = new LogFile(Set("2023-12-18", "mk-2", 1, "Monday", "squat-barbell", "Squat", 345, 3));

        var report = ResultAssert.Succeeded(await ImportAsync(log.Path));

        // The live sequence: the sheet first, then the Strong sessions on their days.
        using var live = new RemodelDatabase();
        ResultAssert.Succeeded(await ImportAsync(log.Path, live));
        var liveSquat = await MovementIdAsync(live);
        await live.CompleteInTurnAsync(Strong(liveSquat, live.UserId));

        Assert.Equal(await live.ChainsAsync(), await _database.ChainsAsync());
        Assert.True(Assert.IsType<RecordsOutcome.Rebuilt>(report.Records).Report.Dropped > 0);

        // The 2025 triple never beat the 2023 one; the 2025 single took the heaviest set from it.
        var heaviest = (await _database.RecordsAsync()).Where(record => record.Kind is RecordKind.HeaviestSet).ToList();
        var older = Assert.Single(heaviest, record => record.AchievedAt.Year == 2023);
        var newer = Assert.Single(heaviest, record => record.AchievedAt.Year == 2025);
        Assert.Equal(new RecordStanding.Beaten(newer.AchievedAt), older.Standing);
        Assert.Equal(new RecordLineage.Replaced(older.PersonalRecordId, older.AchievedAt), newer.Lineage);
        Assert.DoesNotContain(await _database.RecordsAsync(), record =>
            record.Kind == new RecordKind.RepMax(3) && record.AchievedAt.Year == 2025);
    }

    [Fact]
    public async Task A_withdrawn_record_refuses_the_import_and_nothing_is_written()
    {
        var squat = await _database.PlantMovementAsync("Squat", "Barbell");
        await _database.CompleteInTurnAsync(Strong(squat, _database.UserId));
        await using (var context = _database.CreateContext())
        {
            context.PersonalRecords.AsEnumerable()
                .Single(record => record.IsStanding && record.Kind is RecordKind.HeaviestSet)
                .Retract(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            await context.SaveChangesAsync();
        }

        var before = await HeldAsync();
        using var log = new LogFile(Set("2023-12-18", "mk-2", 1, "Monday", "squat-barbell", "Squat", 345, 3));

        Assert.IsType<Problem.Conflict>(ResultAssert.Failed(await ImportAsync(log.Path)));
        Assert.Equal(before, await HeldAsync());
    }

    // ---- Refusals ----

    [Fact]
    public async Task A_not_performed_row_the_log_no_longer_matches_refuses_the_import_and_writes_nothing()
    {
        using var log = new LogFile(
            Set("2023-01-23", "your-program-rpe", 1, "DAY 1", "squat-barbell", "SQUAT", 365, 3),
            Set("2023-01-23", "your-program-rpe", 1, "DAY 1", "squat-barbell", "SQUAT", 296.4, 5));
        var decisions = Decisions(NotPerformed("sheet:your-program-rpe#w1", "2023-01-23", "SQUAT", 296.4, 5, sets: 4));

        var problem = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(await ImportAsync(log.Path, decisions: decisions)));

        Assert.Contains("the log holds 1", Assert.Single(problem.Fields).Message);
        Assert.Empty(await HeldAsync());
    }

    [Fact]
    public async Task An_exercise_or_sheet_the_files_do_not_cover_refuses_the_import_and_writes_nothing()
    {
        using var log = new LogFile(
            Set("2023-12-18", "mk-2", 1, "Monday", "zercher-squat", "Zercher Squat", 225, 5),
            Set("2024-02-12", "block-four", 1, "Day 1", "squat-barbell", "Squat", 365, 2));

        var noMovement = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(await ImportAsync(log.Path)));
        Assert.Equal("block-four", Assert.Single(noMovement.Fields).Field);

        using var programmed = new LogFile(Set("2023-12-18", "mk-2", 1, "Monday", "zercher-squat", "Zercher Squat", 225, 5));
        var unmapped = Assert.IsType<Problem.Invalid>(ResultAssert.Failed(await ImportAsync(programmed.Path)));
        Assert.Equal("zercher-squat", Assert.Single(unmapped.Fields).Field);

        Assert.Empty(await HeldAsync());
    }

    [Fact]
    public async Task An_import_for_an_athlete_who_does_not_exist_is_refused()
    {
        using var log = new LogFile(Set("2023-12-18", "mk-2", 1, "Monday", "squat-barbell", "Squat", 335, 3));
        await using var context = _database.CreateContext();

        var refused = await Importer(context, SquatAndBench, Decisions()).ImportAsync(log.Path, Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(refused));
    }

    // ---- Helpers ----

    private static CompletedTrainingSession[] Strong(Guid squat, Guid userId) =>
    [
        Lifts(userId, new DateTimeOffset(2025, 3, 4, 17, 0, 0, TimeSpan.FromHours(-5)), squat, (300, 3)),
        Lifts(userId, new DateTimeOffset(2025, 6, 4, 17, 0, 0, TimeSpan.FromHours(-4)), squat, (400, 1)),
    ];

    private Task<Result<CoachedSheetImportReport, Problem>> ImportAsync(
        string path, RemodelDatabase? database = null, MovementMap? map = null, SheetDecisions? decisions = null) =>
        ImportIntoAsync(database ?? _database, path, map ?? SquatAndBench, decisions ?? Decisions());

    private static async Task<Result<CoachedSheetImportReport, Problem>> ImportIntoAsync(
        RemodelDatabase database, string path, MovementMap map, SheetDecisions decisions)
    {
        await using var context = database.CreateContext();
        return await Importer(context, map, decisions).ImportAsync(path, database.UserId, CancellationToken.None);
    }

    private async Task<IReadOnlyList<CompletedTrainingSession>> SessionsAsync()
    {
        await using var context = _database.CreateContext();
        return await context.Sessions.OfType<CompletedTrainingSession>().AsNoTracking().ToListAsync();
    }

    private async Task<IReadOnlyList<TrainingProgram>> ProgramsAsync()
    {
        await using var context = _database.CreateContext();
        return await context.Programs.AsNoTracking().ToListAsync();
    }

    private static async Task<Guid> MovementIdAsync(RemodelDatabase database)
    {
        await using var context = database.CreateContext();
        return await context.MovementDatas
            .Where(movement => movement.MovementBase.Name == "Squat" && movement.MovementModifierID == null)
            .Select(movement => movement.MovementDataID)
            .SingleAsync();
    }

    /// <summary>Every session, programme, record and movement id held, so "unchanged" can be told from "replaced".</summary>
    private async Task<IReadOnlyList<Guid>> HeldAsync()
    {
        await using var context = _database.CreateContext();
        return
        [
            .. (await context.Sessions.Select(session => session.TrainingSessionId).ToListAsync())
                .Concat(await context.Programs.Select(program => program.TrainingProgramId).ToListAsync())
                .Concat(await context.PersonalRecords.Select(record => record.PersonalRecordId).ToListAsync())
                .Concat(await context.MovementDatas.Select(movement => movement.MovementDataID).ToListAsync())
                .Order()
        ];
    }
}
