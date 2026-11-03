using System.Globalization;
using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Training;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Strong;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;
using static lionheart.Tests.Strong.StrongExports;

namespace lionheart.Tests.Strong;

/// <summary>
/// The Strong import against a real database: what it stores, that it stores it once, and that the
/// records it establishes are the ones logging each workout live would have.
/// </summary>
public class StrongImportTests : IDisposable
{
    private readonly RemodelDatabase _database = new();

    public void Dispose() => _database.Dispose();

    // ---- What a workout becomes ----

    [Fact]
    public async Task Each_workout_becomes_one_completed_session_over_the_time_it_took()
    {
        var report = ResultAssert.Succeeded(await ImportAsync(FourWorkouts));

        Assert.Equal(4, report.SessionsCreated);
        Assert.Equal(new StrongSetTally(Lifts: 52, Drops: 0, Failures: 3, WarmUps: 1, RestTimers: 20), report.Sets);

        var legs = await SessionOnAsync(new DateOnly(2024, 8, 20));
        var period = Assert.IsType<Interval>(legs.Period);
        Assert.Equal(new DateTimeOffset(2024, 8, 20, 15, 0, 1, TimeSpan.FromHours(-4)), period.Start);
        Assert.Equal(TimeSpan.FromHours(1), period.Duration);
        Assert.Equal(Provenance.User, legs.Provenance);
        Assert.All(legs.Sets, set => Assert.Equal(WeightUnit.Pounds, Assert.IsType<LiftSet>(set).Unit));
    }

    [Fact]
    public async Task A_warm_up_is_kept_in_its_own_words_as_a_freeform_set()
    {
        await ImportAsync(FourWorkouts);

        var push = await SessionOnAsync(new DateOnly(2024, 8, 12));

        var warmUp = Assert.IsType<FreeformSet>(push.Sets[0]);
        Assert.Equal("Warm-up — Bench Press (Barbell): 155 lb × 13", Assert.IsType<Texted>(warmUp.Source).RawText);
        Assert.IsType<LiftSet>(push.Sets[1]);
    }

    [Fact]
    public async Task What_Strong_recorded_with_no_field_here_goes_into_the_notes()
    {
        await ImportAsync(FourWorkouts);

        var legs = await SessionOnAsync(new DateOnly(2024, 8, 20));
        Assert.Equal(
            "RIT Legs\nTaken to failure: Squat (Machine) 365 lb × 7, 365 lb × 3, 315 lb × 5",
            legs.Notes);

        var arms = await SessionOnAsync(new DateOnly(2026, 3, 20));
        Assert.Equal(
            "Bicep Curl (Dumbbell): standing against a beam to have arm behind chest, using a kettlebell",
            arms.Notes);

        var evening = await SessionOnAsync(new DateOnly(2025, 12, 15));
        Assert.StartsWith("horizontal pulling movement \nvertical pressing movement", evening.Notes);
    }

    // ---- The catalogue ----

    [Fact]
    public async Task A_movement_the_athlete_already_has_is_reused_however_he_capitalised_it()
    {
        var curl = await PlantMovementAsync("bicep curl", " DUMBELL ");

        var report = ResultAssert.Succeeded(await ImportAsync(FourWorkouts));

        Assert.DoesNotContain(new MovementName("Bicep Curl", "Dumbell", ""), report.Catalogue.Movements);
        Assert.DoesNotContain("Dumbell", report.Catalogue.Equipment);

        var arms = await SessionOnAsync(new DateOnly(2026, 3, 20));
        Assert.Contains(arms.Sets.OfType<LiftSet>(), set => set.MovementDataId == curl);
    }

    [Fact]
    public async Task Each_new_movement_is_created_once_however_many_workouts_name_it()
    {
        await ImportAsync(FourWorkouts);

        await using var context = _database.CreateContext();
        var identities = await context.MovementDatas
            .Select(movement => new { movement.MovementBaseID, movement.EquipmentID, movement.MovementModifierID })
            .ToListAsync();

        Assert.Equal(identities.Count, identities.Distinct().Count());
        Assert.Equal(1, await context.Equipments.CountAsync(equipment => equipment.Name == "Dumbell"));
    }

    // ---- Re-running ----

    [Fact]
    public async Task Importing_the_same_export_twice_adds_nothing_the_second_time()
    {
        await ImportAsync(FourWorkouts);
        var first = await HeldAsync();

        var second = ResultAssert.Succeeded(await ImportAsync(FourWorkouts));

        Assert.Equal(4, second.AlreadyImported);
        Assert.Equal(0, second.SessionsCreated);
        Assert.Equal(0, second.RecordsEstablished);
        Assert.Empty(second.Catalogue.Movements);
        Assert.Equal(first, await HeldAsync());
    }

    [Fact]
    public async Task A_later_export_adds_only_the_workouts_the_earlier_one_did_not_have()
    {
        using var earlier = new ExportFile(Set("2025-01-10 17:00:00", "Squat (Barbell)", "1", 225, 5));
        using var later = new ExportFile(
            Set("2025-01-10 17:00:00", "Squat (Barbell)", "1", 225, 5),
            Set("2025-02-10 17:00:00", "Squat (Barbell)", "1", 245, 5));
        var map = Map(("Squat (Barbell)", "Squat", "Barbell", ""));

        await ImportAsync(earlier.Path, map);
        var report = ResultAssert.Succeeded(await ImportAsync(later.Path, map));

        Assert.Equal((1, 1), (report.AlreadyImported, report.SessionsCreated));
        Assert.Equal(245, StandingAchievement<Achievement.HeaviestSet>(await StandingAsync()).Lift.Weight);
    }

    [Fact]
    public void A_workout_imports_under_the_same_id_on_every_run_and_for_no_other_athlete()
    {
        // Pinned, because this id is the whole "already imported" check: a change to how it is
        // derived would make the next run import the athlete's history a second time.
        var key = new StrongWorkoutKey(new DateTime(2024, 7, 23, 12, 52, 19), "Tester workout");
        var athlete = Guid.Parse("5f72c72f-21d9-49e9-9ee1-014e22d6b0d4");

        // The expected value is Python's uuid5 over the same namespace and name, an implementation
        // independent of this one.
        Assert.Equal(Guid.Parse("c2cbf534-1114-5ca4-9dbd-26b9bc2866dc"), key.SessionIdFor(athlete));
        Assert.NotEqual(key.SessionIdFor(athlete), key.SessionIdFor(Guid.NewGuid()));
    }

    [Fact]
    public void A_workout_id_does_not_move_with_the_machine_locale()
    {
        // Finnish separates hours and minutes with a full stop, which a date format's ':' follows.
        var key = new StrongWorkoutKey(new DateTime(2024, 7, 23, 12, 52, 19), "Tester workout");
        var athlete = Guid.Parse("5f72c72f-21d9-49e9-9ee1-014e22d6b0d4");
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fi-FI");
            Assert.Equal(Guid.Parse("c2cbf534-1114-5ca4-9dbd-26b9bc2866dc"), key.SessionIdFor(athlete));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    // ---- Records ----

    [Fact]
    public async Task A_warm_up_sets_no_record_however_many_reps_it_had()
    {
        // The bench warm-up was 155 x 13 and no working set went past eight reps. Counted as a
        // lift it would hold the nine- to twelve-rep maxes and the best set volume.
        await ImportAsync(FourWorkouts);

        var bench = await MovementIdAsync("Bench Press", "Barbell", modifier: null);
        var benchRecords = (await StandingAsync()).Where(record => record.Subject == new RecordSubject.Movement(bench)).ToList();

        Assert.Equal(8, benchRecords.Select(record => record.Achievement).OfType<Achievement.RepMax>().Max(max => max.Reps));
        Assert.Equal(new LiftPerformance(170, WeightUnit.Pounds, 8),
            Assert.Single(benchRecords.Select(record => record.Achievement).OfType<Achievement.BestSetVolume>()).Lift);
    }

    [Fact]
    public async Task Sets_to_failure_count_toward_records_like_any_set_performed()
    {
        using var export = new ExportFile(
            Set("2025-01-10 17:00:00", "Squat (Machine)", "1", 405, 5),
            Set("2025-01-10 17:00:00", "Squat (Machine)", "F", 365, 7),
            Set("2025-01-10 17:00:00", "Squat (Machine)", "D", 315, 9));

        await ImportAsync(export.Path, Map(("Squat (Machine)", "Squat", "Machine", "")));

        var maxes = (await StandingAsync()).Select(record => record.Achievement).OfType<Achievement.RepMax>().ToList();
        Assert.Equal(365, maxes.Single(max => max.Reps == 7).Lift.Weight);
        Assert.Equal(315, maxes.Single(max => max.Reps == 9).Lift.Weight);
    }

    [Fact]
    public async Task Workouts_listed_newest_first_still_chain_their_records_oldest_first()
    {
        using var export = new ExportFile(
            Set("2025-02-10 17:00:00", "Squat (Barbell)", "1", 245, 5),
            Set("2025-01-10 17:00:00", "Squat (Barbell)", "1", 225, 5));

        await ImportAsync(export.Path, Map(("Squat (Barbell)", "Squat", "Barbell", "")));

        var heaviest = StandingRecord<Achievement.HeaviestSet>(await StandingAsync());
        Assert.Equal(245, Assert.IsType<Achievement.HeaviestSet>(heaviest.Achievement).Lift.Weight);
        Assert.Equal(new DateTime(2025, 1, 10, 22, 0, 0), Assert.IsType<RecordLineage.Replaced>(heaviest.Lineage).AchievedAt);
    }

    [Fact]
    public async Task The_records_an_import_establishes_are_those_logging_each_workout_live_would_have()
    {
        // The claim the batch rests on: one detector pass over every workout, oldest first, ends
        // where completing them one at a time on their own days does. The second database takes
        // the same history one workout per run, in date order — the live sequence.
        var workouts = new[]
        {
            Set("2025-01-10 17:00:00", "Squat (Barbell)", "1", 225, 5),
            Set("2025-01-10 17:00:00", "Squat (Barbell)", "2", 235, 3),
            Set("2025-02-10 17:00:00", "Squat (Barbell)", "1", 245, 2),
            Set("2025-02-10 17:00:00", "Squat (Barbell)", "2", 205, 8),
            Set("2025-03-10 17:00:00", "Squat (Barbell)", "1", 230, 5),
            Set("2025-03-10 17:00:00", "Squat (Barbell)", "D", 185, 10),
            Set("2025-03-10 17:00:00", "Bench Press (Barbell)", "1", 185, 5),
        };
        var map = Map(("Squat (Barbell)", "Squat", "Barbell", ""), ("Bench Press (Barbell)", "Bench Press", "Barbell", ""));

        using var whole = new ExportFile(workouts);
        await ImportAsync(whole.Path, map);

        using var live = new RemodelDatabase();
        foreach (var day in workouts.GroupBy(row => row[..10]).OrderBy(day => day.Key))
        {
            using var oneWorkout = new ExportFile([.. day]);
            await using var context = live.CreateContext();
            ResultAssert.Succeeded(await Importer(context, map).ImportAsync(oneWorkout.Path, live.UserId, CancellationToken.None));
        }

        Assert.Equal(await ChainsAsync(live), await ChainsAsync(_database));
    }

    // ---- Refusals: nothing written ----

    [Fact]
    public async Task A_standing_record_set_after_the_history_being_added_refuses_the_import()
    {
        // A record already standing from June would be compared against a January workout, and
        // its chain would claim it beat a session older than itself.
        var squat = await PlantMovementAsync("Squat", "Barbell");
        await PlantRecordAsync(squat, WeightUnit.Pounds, new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc));
        using var export = new ExportFile(Set("2025-01-10 17:00:00", "Squat (Barbell)", "1", 225, 5));

        var refused = await ImportAsync(export.Path, Map(("Squat (Barbell)", "Squat", "Barbell", "")));

        var conflict = Assert.IsType<Problem.Conflict>(ResultAssert.Failed(refused));
        Assert.Contains("Squat / Barbell: heaviestSet in Pounds, set 2025-06-01", conflict.Description);
        Assert.Equal((0, 1, 1), await CountsAsync());
    }

    [Fact]
    public async Task A_standing_record_this_history_does_not_contest_does_not_refuse_it()
    {
        // Same movement and kind, but held in kilograms: pounds sets can neither beat it nor be
        // compared against it, so the order of the two histories cannot matter.
        var squat = await PlantMovementAsync("Squat", "Barbell");
        await PlantRecordAsync(squat, WeightUnit.Kilograms, new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc));
        using var export = new ExportFile(Set("2025-01-10 17:00:00", "Squat (Barbell)", "1", 225, 5));

        ResultAssert.Succeeded(await ImportAsync(export.Path, Map(("Squat (Barbell)", "Squat", "Barbell", ""))));

        Assert.Equal(2, (await StandingAsync()).Count(record => record.Kind is RecordKind.HeaviestSet));
    }

    [Fact]
    public async Task An_exercise_the_map_does_not_cover_refuses_the_import_and_writes_nothing()
    {
        using var export = new ExportFile(
            Set("2025-01-10 17:00:00", "Squat (Barbell)", "1", 225, 5),
            Set("2025-01-10 17:00:00", "Zercher Squat", "1", 185, 5));

        var refused = await ImportAsync(export.Path, Map(("Squat (Barbell)", "Squat", "Barbell", "")));

        Assert.Equal("Zercher Squat", Assert.Single(Assert.IsType<Problem.Invalid>(ResultAssert.Failed(refused)).Fields).Field);
        Assert.Equal((0, 0, 0), await CountsAsync());
    }

    [Fact]
    public async Task An_import_for_an_athlete_who_does_not_exist_is_refused()
    {
        await using var context = _database.CreateContext();

        var refused = await Importer(context, ResultAssert.Succeeded(StrongMovementMap.Shipped()))
            .ImportAsync(FourWorkouts, Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<Problem.NotFound>(ResultAssert.Failed(refused));
    }

    // ---- Helpers ----

    private Task<Result<StrongImportReport, Problem>> ImportAsync(string path) =>
        ImportAsync(path, ResultAssert.Succeeded(StrongMovementMap.Shipped()));

    private async Task<Result<StrongImportReport, Problem>> ImportAsync(string path, StrongMovementMap map)
    {
        await using var context = _database.CreateContext();
        return await Importer(context, map).ImportAsync(path, _database.UserId, CancellationToken.None);
    }

    private async Task<CompletedTrainingSession> SessionOnAsync(DateOnly day)
    {
        await using var context = _database.CreateContext();
        var sessions = await context.Sessions.OfType<CompletedTrainingSession>().AsNoTracking().ToListAsync();
        return sessions.Single(session => session.Date == day);
    }

    private async Task<Guid> PlantMovementAsync(string movementBase, string equipment)
    {
        await using var context = _database.CreateContext();
        var movement = new MovementData
        {
            MovementDataID = Guid.NewGuid(),
            UserID = _database.UserId,
            EquipmentID = Guid.NewGuid(),
            Equipment = new Equipment { EquipmentID = Guid.NewGuid(), Name = equipment, UserID = _database.UserId },
            MovementBaseID = Guid.NewGuid(),
            MovementBase = new MovementBase
            {
                MovementBaseID = Guid.NewGuid(), Name = movementBase, UserID = _database.UserId,
                Description = string.Empty, MuscleGroups = [],
            },
        };
        movement.EquipmentID = movement.Equipment.EquipmentID;
        movement.MovementBaseID = movement.MovementBase.MovementBaseID;
        context.MovementDatas.Add(movement);
        await context.SaveChangesAsync();
        return movement.MovementDataID;
    }

    private async Task PlantRecordAsync(Guid movement, WeightUnit unit, DateTime achievedAt)
    {
        await using var context = _database.CreateContext();
        context.PersonalRecords.Add(PersonalRecord.FirstOf(
            Guid.NewGuid(), _database.UserId, new RecordSubject.Movement(movement),
            new Achievement.HeaviestSet(new LiftPerformance(300, unit, 1)), achievedAt));
        await context.SaveChangesAsync();
    }

    private async Task<Guid> MovementIdAsync(string movementBase, string equipment, string? modifier)
    {
        await using var context = _database.CreateContext();
        return await context.MovementDatas
            .Where(movement => movement.MovementBase.Name == movementBase
                               && movement.Equipment.Name == equipment
                               && (modifier == null ? movement.MovementModifierID == null : movement.MovementModifier!.Name == modifier))
            .Select(movement => movement.MovementDataID)
            .SingleAsync();
    }

    private async Task<IReadOnlyList<PersonalRecord>> StandingAsync()
    {
        await using var context = _database.CreateContext();
        return (await context.PersonalRecords.AsNoTracking().ToListAsync()).Where(record => record.IsStanding).ToList();
    }

    private static PersonalRecord StandingRecord<T>(IEnumerable<PersonalRecord> records) where T : Achievement =>
        Assert.Single(records, record => record.Achievement is T);

    private static T StandingAchievement<T>(IEnumerable<PersonalRecord> records) where T : Achievement =>
        (T)StandingRecord<T>(records).Achievement;

    /// <summary>Sessions, movements and records held, for asserting a refusal wrote nothing.</summary>
    private async Task<(int Sessions, int Movements, int Records)> CountsAsync()
    {
        await using var context = _database.CreateContext();
        return (await context.Sessions.CountAsync(), await context.MovementDatas.CountAsync(), await context.PersonalRecords.CountAsync());
    }

    /// <summary>Every session, record and movement id held, so "unchanged" can be told from "replaced".</summary>
    private async Task<IReadOnlyList<Guid>> HeldAsync()
    {
        await using var context = _database.CreateContext();
        return
        [
            .. (await context.Sessions.Select(session => session.TrainingSessionId).ToListAsync())
                .Concat(await context.PersonalRecords.Select(record => record.PersonalRecordId).ToListAsync())
                .Concat(await context.MovementDatas.Select(movement => movement.MovementDataID).ToListAsync())
                .Order()
        ];
    }

    /// <summary>
    /// Every record in a database, described without its ids, which two databases never share:
    /// the movement by name, what it holds, when, where it stands, and when the one it replaced was set.
    /// </summary>
    /// <remarks>
    /// Instants are compared as instants. A predecessor's date is copied from the record in memory,
    /// which is UTC-kinded when the record was made in the same batch and unkinded when it was read
    /// back from storage — the same moment either way, and both read back as UTC by the clients.
    /// </remarks>
    private static async Task<IReadOnlyList<string>> ChainsAsync(RemodelDatabase database)
    {
        await using var context = database.CreateContext();
        var names = await context.MovementDatas
            .Select(movement => new { movement.MovementDataID, movement.MovementBase.Name })
            .ToDictionaryAsync(movement => movement.MovementDataID, movement => movement.Name);
        var records = await context.PersonalRecords.AsNoTracking().ToListAsync();

        return
        [
            .. records
                .Select(record => string.Join(" | ",
                    record.Subject is RecordSubject.Movement movement ? names[movement.MovementDataId] : "trail",
                    record.Achievement,
                    Instant(record.AchievedAt),
                    record.Standing is RecordStanding.Beaten beaten ? $"beaten {Instant(beaten.At)}" : record.Standing.GetType().Name,
                    record.Lineage is RecordLineage.Replaced replaced ? Instant(replaced.AchievedAt) : "first"))
                .Order(StringComparer.Ordinal)
        ];
    }

    private static string Instant(DateTime at) => DateTime.SpecifyKind(at, DateTimeKind.Utc).ToString("O");
}
