using lionheart.Functional;
using lionheart.Model.InjuryManagement;
using lionheart.Model.Training;
using lionheart.Model.Training.Focus;
using lionheart.Model.Training.Patterns;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Routines;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Training.Movement.Library;
using lionheart.Services.Training.Patterns;
using lionheart.Services.Training.PersonalRecords;
using lionheart.Tests.Persistence;
using lionheart.Tests.Support;
using Microsoft.EntityFrameworkCore;
using static lionheart.Tests.Support.TrainingHistory;

namespace lionheart.Tests.MovementLibrary;

/// <summary>
/// The tidy against a real database: a merge reaches everything that names the merged movement,
/// a dry run writes nothing, a second run has nothing to do, and a refusal leaves the library as
/// it was.
/// </summary>
public class MovementLibraryTidyTests : IDisposable
{
    private static readonly DateTimeOffset January = new(2025, 1, 10, 17, 0, 0, TimeSpan.FromHours(-5));

    /// <summary>A pull-up bar is bodyweight: renaming the one onto the other merges the two pull-ups.</summary>
    private const string BarIsBodyweight = """{ "equipment": [ { "from": "Pull-Up Bar", "to": "Bodyweight" } ] }""";

    private readonly RemodelDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task A_merge_moves_every_set_pin_routine_and_injury_note_and_replays_the_records()
    {
        var kept = await PlantAsync("Pull Up", "Bodyweight");
        var gone = await PlantAsync("Pull Up", "Pull-Up Bar");
        await _database.CompleteInTurnAsync(
            Lifts(_database.UserId, January, gone, (25, 8), (35, 6)),
            Lifts(_database.UserId, January.AddDays(7), kept, (30, 8)));
        await PlantAsync(context =>
        {
            context.Sessions.Add(Planned(gone));
            context.FocusPins.Add(Pinned(gone));
            context.Routines.Add(Some.UpperDay(_database.UserId, gone));
            context.Injuries.Add(Hurt(gone, kept));
        });

        var report = ResultAssert.Succeeded(await TidyAsync(BarIsBodyweight, TidyRun.Write));

        Assert.True(report.Written);
        await using var context = _database.CreateContext();
        Assert.Equal([kept], await context.MovementDatas.Select(movement => movement.MovementDataID).ToListAsync());
        Assert.Equal(["Bodyweight"], await context.Equipments.Select(row => row.Name).ToListAsync());

        var sessions = await context.Sessions.ToListAsync();
        Assert.All(sessions.OfType<CompletedTrainingSession>().SelectMany(session => session.Sets).OfType<LiftSet>(),
            set => Assert.Equal(kept, set.MovementDataId));
        Assert.Equal(kept, Assert.IsType<LiftPrescription>(Assert.Single(sessions.OfType<PlannedTrainingSession>().Single().Prescriptions)).MovementDataId);
        Assert.Equal(kept, (await context.FocusPins.SingleAsync()).MovementDataId);
        Assert.Equal(kept, Assert.Single((await context.Routines.SingleAsync()).Movements).MovementDataId);
        Assert.Equal([kept], (await context.InjuryEvents.SingleAsync()).MovementIDs);

        // The heaviest set was on the merged-away movement; replayed, it stands on the one kept.
        Assert.IsType<RecordReplay.Replayed>(report.Records);
        var records = await context.PersonalRecords.AsNoTracking().ToListAsync();
        Assert.All(records, record => Assert.Equal(new RecordSubject.Movement(kept), record.Subject));
        Assert.Contains(records, record => record.IsStanding && record.Achievement is Achievement.HeaviestSet { Lift.Weight: 35 });
    }

    [Fact]
    public async Task A_dry_run_reports_what_a_write_would_do_and_writes_nothing()
    {
        var kept = await PlantAsync("Pull Up", "Bodyweight");
        var gone = await PlantAsync("Pull Up", "Pull-Up Bar");
        await _database.CompleteInTurnAsync(Lifts(_database.UserId, January, gone, (25, 8)));
        var before = await FingerprintAsync();

        var report = ResultAssert.Succeeded(await TidyAsync(BarIsBodyweight, TidyRun.DryRun));

        Assert.False(report.Written);
        Assert.Equal(new LibraryCounts(1, 1, 1, 0), report.After);
        Assert.Equal(new Dictionary<Guid, Guid> { [gone] = kept }, report.Plan.Merges);
        Assert.IsType<RecordReplay.Replayed>(report.Records);
        Assert.Equal(before, await FingerprintAsync());
    }

    [Fact]
    public async Task A_second_run_finds_nothing_to_do()
    {
        await PlantAsync("Pull Up", "Bodyweight");
        await PlantAsync("Leg Curl", "Seated Leg Curl");
        const string rules = """
            { "equipment": [ { "from": "Pull-Up Bar", "to": "Bodyweight" } ],
              "movements": [ { "from": { "base": "Leg Curl", "equipment": "Seated Leg Curl" },
                               "to": { "base": "Leg Curl", "equipment": "Machine", "modifier": "seated" } } ],
              "seed": [ { "base": "Push Up", "equipment": "Bodyweight" } ] }
            """;
        ResultAssert.Succeeded(await TidyAsync(rules, TidyRun.Write));
        var after = await FingerprintAsync();

        var again = ResultAssert.Succeeded(await TidyAsync(rules, TidyRun.Write));

        Assert.True(again.Plan.IsEmpty);
        Assert.False(again.Written);
        Assert.Equal(after, await FingerprintAsync());
        Assert.Contains(after, line => line.EndsWith(" Leg Curl / Machine / seated"));
        Assert.Contains(after, line => line.EndsWith(" Push Up / Bodyweight"));
    }

    [Fact]
    public async Task A_routine_holding_both_movements_of_a_merge_refuses_and_nothing_is_written()
    {
        // Which of the two set counts the routine means is the athlete's call, not the tidy's.
        var kept = await PlantAsync("Pull Up", "Bodyweight");
        var gone = await PlantAsync("Pull Up", "Pull-Up Bar");
        await PlantAsync(context => context.Routines.Add(Some.UpperDay(_database.UserId, kept, gone)));
        var before = await FingerprintAsync();

        var problem = Assert.IsType<Problem.Conflict>(ResultAssert.Failed(await TidyAsync(BarIsBodyweight, TidyRun.Write)));

        Assert.Equal("routine 'Upper day' holds two movements that merge into one; take one out in the app first.", problem.What);
        Assert.Equal(before, await FingerprintAsync());
    }

    [Fact]
    public async Task A_folded_base_hands_its_stated_classification_to_the_base_it_folds_into()
    {
        await PlantAsync("Pushup", "Bodyweight");
        await PlantAsync("Push Up", "Barbell");
        var stated = ResultAssert.Succeeded(MovementClassification.Of(MovementPattern.HorizontalPush, [MuscleGroup.Chest], []));
        await PlantAsync(context => context.MovementBases.Single(row => row.Name == "Pushup").State(stated));

        ResultAssert.Succeeded(await TidyAsync("""{ "bases": [ { "from": "Pushup", "to": "Push Up" } ] }""", TidyRun.Write));

        await using var context = _database.CreateContext();
        var pushUp = Assert.Single(await context.MovementBases.ToListAsync());
        Assert.Equal("Push Up", pushUp.Name);
        Assert.Equal(new StatedClassification.Stated(stated), pushUp.Classification);
    }

    [Fact]
    public async Task Another_athletes_library_is_left_as_it_was()
    {
        var stranger = await _database.PlantAthleteAsync();
        await PlantAsync("Pull Up", "Pull-Up Bar", stranger);
        await PlantAsync("Pull Up", "Pull-Up Bar");

        ResultAssert.Succeeded(await TidyAsync(BarIsBodyweight, TidyRun.Write));

        await using var context = _database.CreateContext();
        Assert.Equal(["Pull-Up Bar"], await context.Equipments.Where(row => row.UserID == stranger).Select(row => row.Name).ToListAsync());
        Assert.Equal(["Bodyweight"], await context.Equipments.Where(row => row.UserID == _database.UserId).Select(row => row.Name).ToListAsync());
    }

    // ---- Running it ----

    private async Task<Result<TidyReport, Problem>> TidyAsync(string rules, TidyRun run)
    {
        await using var context = _database.CreateContext();
        var records = new PersonalRecordRebuild(context, new PersonalRecordDetector(context, TimeProvider.System));
        return await new MovementLibraryTidy(
                context, ResultAssert.Succeeded(MovementLibraryRules.Parse(rules)), records, MovementPatternMap.Shipped,
                new FixedClock(January.AddYears(1)))
            .TidyAsync(_database.UserId, run, CancellationToken.None);
    }

    /// <summary>Everything a tidy can touch, as text: every movement by name, every part row, and every session's sets.</summary>
    private async Task<IReadOnlyList<string>> FingerprintAsync()
    {
        await using var context = _database.CreateContext();
        var movements = await context.MovementDatas.AsNoTracking()
            .Select(movement => movement.MovementDataID + " " + movement.MovementBase.Name + " / " + movement.Equipment.Name
                                + (movement.MovementModifier == null ? string.Empty : " / " + movement.MovementModifier.Name))
            .ToListAsync();
        var parts = (await context.MovementBases.Select(row => "base " + row.Name).ToListAsync())
            .Concat(await context.Equipments.Select(row => "equipment " + row.Name).ToListAsync())
            .Concat(await context.MovementModifiers.Select(row => "modifier " + row.Name).ToListAsync());
        var sets = (await context.Sessions.AsNoTracking().ToListAsync())
            .SelectMany(session => session is CompletedTrainingSession completed ? completed.Sets.OfType<LiftSet>() : [])
            .Select(set => $"set on {set.MovementDataId}");
        var routines = (await context.Routines.AsNoTracking().ToListAsync())
            .SelectMany(routine => routine.Movements.Select(movement => $"routine {movement.MovementDataId}"));
        var records = await context.PersonalRecords.CountAsync();

        return [.. movements, .. parts, .. sets, .. routines, $"{records} records"];
    }

    // ---- Planting a library ----

    /// <summary>A movement, reusing any part row the athlete already has by that exact spelling.</summary>
    private async Task<Guid> PlantAsync(string movementBase, string equipment, Guid? owner = null)
    {
        var userId = owner ?? _database.UserId;
        await using var context = _database.CreateContext();
        var baseRow = await context.MovementBases.FirstOrDefaultAsync(row => row.UserID == userId && row.Name == movementBase)
                      ?? new MovementBase { MovementBaseID = Guid.NewGuid(), Name = movementBase, UserID = userId, Description = string.Empty };
        var equipmentRow = await context.Equipments.FirstOrDefaultAsync(row => row.UserID == userId && row.Name == equipment)
                           ?? new Equipment { EquipmentID = Guid.NewGuid(), Name = equipment, UserID = userId };
        var movement = new MovementData
        {
            MovementDataID = Guid.NewGuid(),
            UserID = userId,
            MovementBaseID = baseRow.MovementBaseID,
            MovementBase = baseRow,
            EquipmentID = equipmentRow.EquipmentID,
            Equipment = equipmentRow,
            CreatedAt = January.UtcDateTime,
        };
        context.MovementDatas.Add(movement);
        await context.SaveChangesAsync();
        return movement.MovementDataID;
    }

    private async Task PlantAsync(Action<lionheart.Data.ModelContext> plant)
    {
        await using var context = _database.CreateContext();
        plant(context);
        await context.SaveChangesAsync();
    }

    private PlannedTrainingSession Planned(Guid movement) =>
        new(new DateOnly(2025, 2, 1), [new LiftPrescription(movement, 8, 30, WeightUnit.Pounds, Rpe.Unrated)])
        {
            TrainingSessionId = Guid.NewGuid(),
            UserId = _database.UserId,
            Notes = string.Empty,
            Provenance = Provenance.User,
            Membership = new Standalone(),
            Origin = new RoutineOrigin.AdHoc(),
            Source = new SessionSource.AthleteLog(),
            CreatedAt = January.UtcDateTime,
        };

    private FocusPin Pinned(Guid movement) => FocusPin.Pin(
        Guid.NewGuid(), _database.UserId, movement, new FocusRole.Emphasis(),
        ResultAssert.Succeeded(FocusHorizon.Of(new DateOnly(2025, 1, 1), new DateOnly(2025, 3, 1))
            .Bind(horizon => FocusTerms.Of(horizon, 2, new FocusTarget.None()))),
        January.UtcDateTime);

    private Injury Hurt(params Guid[] movements) => new()
    {
        UserID = _database.UserId,
        Name = "A tweaked elbow",
        InjuryDate = new DateOnly(2025, 1, 12),
        InjuryEvents = [new InjuryEvent { MovementIDs = [.. movements], PainLevel = 3 }],
    };
}
