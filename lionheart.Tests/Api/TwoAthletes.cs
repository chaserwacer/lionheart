using lionheart.Data;
using lionheart.Model.Training;
using lionheart.Model.User;
using lionheart.Services;
using lionheart.Services.BodyComposition;
using lionheart.Services.Cardio;
using lionheart.Services.Health.Import;
using lionheart.Services.Health.Recordings;
using lionheart.Services.Contexts;
using lionheart.Services.Training.Balance;
using lionheart.Services.Training.Focus;
using lionheart.Services.Training.Patterns;
using lionheart.Services.Training.PersonalRecords;
using lionheart.Services.Training.Programs;
using lionheart.Services.Training.Routines;
using lionheart.Services.Training.Sessions;
using lionheart.Tests.Persistence;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Tests.Api;

/// <summary>
/// A <see cref="RemodelDatabase"/> with a second athlete seeded, and the services under test built
/// over a fresh context per call.
/// </summary>
/// <remarks>
/// Two real athletes, not one athlete and an invented id: sessions, programs and contexts all
/// carry a foreign key to LionheartUser, so a stranger whose row does not exist could not own the
/// records an isolation test needs them to own. A fresh context per call is what makes a read
/// after a write a real round trip through storage rather than a look at the change tracker.
/// </remarks>
public sealed class TwoAthletes : IDisposable
{
    private readonly RemodelDatabase _database = new();

    public TwoAthletes()
    {
        StrangerId = Guid.NewGuid();
        Owner = new IdentityUser { Id = OwnerId.ToString() };
        Stranger = new IdentityUser { Id = StrangerId.ToString() };
        Unprofiled = new IdentityUser { Id = Guid.NewGuid().ToString() };

        using var context = _database.CreateContext();
        var identity = new IdentityUser("stranger@lionheart.local");
        context.Users.Add(identity);
        context.LionheartUsers.Add(new LionheartUser
        {
            UserID = StrangerId,
            IdentityUser = identity,
            Name = "Stranger",
        });
        context.SaveChanges();
    }

    public Guid OwnerId => _database.UserId;

    public Guid StrangerId { get; }

    /// <summary>
    /// The athlete as a service sees them. Only the id is read at the boundary, so this need not be
    /// the same instance the database seeded — it has to carry the same id.
    /// </summary>
    public IdentityUser Owner { get; }

    public IdentityUser Stranger { get; }

    /// <summary>
    /// An identity that registered and never created a profile — deliberately given no
    /// LionheartUser row, which is the state every new account is in until it creates one.
    /// </summary>
    public IdentityUser Unprofiled { get; }

    public ModelContext CreateContext() => _database.CreateContext();

    /// <remarks>
    /// Wired with the real detector, not a stand-in. Completing a session establishes records in
    /// the same save, so a fake here would leave every completion test asserting against a write
    /// path the application does not have.
    /// </remarks>
    public async Task<T> SessionsAsync<T>(Func<ITrainingSessionService, Task<T>> act)
    {
        using var context = CreateContext();
        var detector = new PersonalRecordDetector(context, TimeProvider.System);
        return await act(new TrainingSessionService(
            context, TimeProvider.System, detector, new PersonalRecordRebuild(context, detector)));
    }

    public async Task<T> RecordsAsync<T>(Func<IPersonalRecordService, Task<T>> act)
    {
        using var context = CreateContext();
        return await act(new PersonalRecordService(context, TimeProvider.System));
    }

    public async Task<T> ProgramsAsync<T>(Func<ITrainingProgramService, Task<T>> act)
    {
        using var context = CreateContext();
        return await act(new TrainingProgramService(context, TimeProvider.System));
    }

    public async Task<T> FocusAsync<T>(TimeProvider clock, Func<IFocusPinService, Task<T>> act)
    {
        using var context = CreateContext();
        return await act(new FocusPinService(context, clock, MovementPatternMap.Shipped));
    }

    public async Task<T> ContextsAsync<T>(Func<IContextService, Task<T>> act)
    {
        using var context = CreateContext();
        return await act(new ContextService(context, TimeProvider.System));
    }

    public async Task<T> RoutinesAsync<T>(Func<IRoutineService, Task<T>> act)
    {
        using var context = CreateContext();
        return await act(new RoutineService(context, TimeProvider.System));
    }

    public async Task<T> PreviousPerformancesAsync<T>(Func<IPreviousPerformanceService, Task<T>> act)
    {
        using var context = CreateContext();
        return await act(new PreviousPerformanceService(context));
    }

    public async Task<T> BodyweightAsync<T>(Func<IBodyweightService, Task<T>> act)
    {
        using var context = CreateContext();
        return await act(new BodyweightService(context));
    }

    public async Task<T> ClassificationAsync<T>(Func<IMovementClassificationService, Task<T>> act)
    {
        using var context = CreateContext();
        return await act(new MovementClassificationService(context, MovementPatternMap.Shipped));
    }

    public async Task<T> VolumeAsync<T>(Func<ITrainingVolumeService, Task<T>> act)
    {
        using var context = CreateContext();
        return await act(new TrainingVolumeService(context, MovementPatternMap.Shipped));
    }

    public async Task<T> BodyCompositionAsync<T>(Func<IBodyCompositionService, Task<T>> act)
    {
        using var context = CreateContext();
        return await act(new BodyCompositionService(context));
    }

    /// <remarks>Wired with the real detector and rebuild, for the reason <see cref="SessionsAsync{T}"/> is.</remarks>
    public async Task<T> HealthImportAsync<T>(Func<IHealthWorkoutImportService, Task<T>> act)
    {
        using var context = CreateContext();
        var detector = new PersonalRecordDetector(context, TimeProvider.System);
        return await act(new HealthWorkoutImportService(
            context, TimeProvider.System, detector, new PersonalRecordRebuild(context, detector)));
    }

    public async Task<T> RecordedWorkoutsAsync<T>(Func<IRecordedWorkoutService, Task<T>> act)
    {
        using var context = CreateContext();
        return await act(new RecordedWorkoutService(context, new CardioReportService(context)));
    }

    public async Task<T> MovementDataAsync<T>(Func<IMovementDataService, Task<T>> act)
    {
        using var context = CreateContext();
        return await act(new MovementDataService(context, new MovementModifierService(context), MovementPatternMap.Shipped));
    }

    /// <summary>
    /// Plants a real movement for an athlete, because a record's subject is only eligible when the
    /// movement behind it is theirs.
    /// </summary>
    public async Task<Guid> PlantMovementAsync(Guid userId, string name)
    {
        var movementDataId = Guid.NewGuid();
        var equipment = new Equipment { EquipmentID = Guid.NewGuid(), Name = "Barbell", UserID = userId };
        var movementBase = new MovementBase
        {
            MovementBaseID = Guid.NewGuid(),
            Name = name,
            UserID = userId,
            Description = string.Empty,
        };

        await PlantAsync(context => context.MovementDatas.Add(new MovementData
        {
            MovementDataID = movementDataId,
            UserID = userId,
            EquipmentID = equipment.EquipmentID,
            Equipment = equipment,
            MovementBaseID = movementBase.MovementBaseID,
            MovementBase = movementBase,
        }));

        return movementDataId;
    }

    /// <summary>Plants a record no route can create — another athlete's, or a state only reachable by transition.</summary>
    public async Task PlantAsync(Action<ModelContext> add)
    {
        using var context = CreateContext();
        add(context);
        await context.SaveChangesAsync();
    }

    public void Dispose() => _database.Dispose();
}
