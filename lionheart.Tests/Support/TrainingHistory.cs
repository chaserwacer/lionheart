using lionheart.Model.Analysis;
using lionheart.Model.Training;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Model.User;
using lionheart.Services.Training.PersonalRecords;
using lionheart.Tests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Tests.Support;

/// <summary>
/// Training history for the import and record tests: movements in an athlete's catalogue, sessions
/// completed the way the app completes them, and the record chains a database ends up holding.
/// </summary>
internal static class TrainingHistory
{
    /// <summary>A movement in the athlete's catalogue, by the names an import matches on.</summary>
    public static async Task<Guid> PlantMovementAsync(
        this RemodelDatabase database, string movementBase, string equipment, Guid? owner = null)
    {
        var userId = owner ?? database.UserId;
        await using var context = database.CreateContext();
        var movement = new MovementData
        {
            MovementDataID = Guid.NewGuid(),
            UserID = userId,
            EquipmentID = Guid.NewGuid(),
            Equipment = new Equipment { EquipmentID = Guid.NewGuid(), Name = equipment, UserID = userId },
            MovementBaseID = Guid.NewGuid(),
            MovementBase = new MovementBase
            {
                MovementBaseID = Guid.NewGuid(), Name = movementBase, UserID = userId,
                Description = string.Empty,
            },
        };
        movement.EquipmentID = movement.Equipment.EquipmentID;
        movement.MovementBaseID = movement.MovementBase.MovementBaseID;
        context.MovementDatas.Add(movement);
        await context.SaveChangesAsync();
        return movement.MovementDataID;
    }

    /// <summary>A completed session of pounds lifts on one movement, an hour long.</summary>
    public static CompletedTrainingSession Lifts(
        Guid userId, DateTimeOffset start, Guid movement, params (double Weight, int Reps)[] sets) =>
        new([.. sets.Select(SetEntry (set) => new LiftSet(movement, set.Reps, set.Weight, WeightUnit.Pounds, Rpe.Unrated, SetKind.Working)
            {
                Source = new Formed(),
                Facets = [],
            })],
            new Interval(start, start.AddHours(1)),
            new EffortNotReported(),
            new NotDecorated())
        {
            TrainingSessionId = Guid.NewGuid(),
            UserId = userId,
            Notes = string.Empty,
            Provenance = Provenance.User,
            Membership = new Standalone(),
            Origin = new RoutineOrigin.AdHoc(),
            Source = new SessionSource.AthleteLog(),
            CreatedAt = start.UtcDateTime,
        };

    /// <summary>
    /// Completes each session in its own commit, in the order given, detecting its records against
    /// what stands — what logging them live in that order does.
    /// </summary>
    public static async Task CompleteInTurnAsync(this RemodelDatabase database, params CompletedTrainingSession[] sessions)
    {
        foreach (var session in sessions)
        {
            await using var context = database.CreateContext();
            await new PersonalRecordDetector(context, TimeProvider.System).StageAsync([session], CancellationToken.None);
            context.Sessions.Add(session);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>A second athlete in the database, for showing one athlete's work leaves the other's alone.</summary>
    public static async Task<Guid> PlantAthleteAsync(this RemodelDatabase database)
    {
        await using var context = database.CreateContext();
        var identity = new IdentityUser($"stranger-{Guid.NewGuid():N}@lionheart.local");
        var athlete = new LionheartUser { UserID = Guid.NewGuid(), IdentityUser = identity, Name = "Stranger" };
        context.Users.Add(identity);
        context.LionheartUsers.Add(athlete);
        await context.SaveChangesAsync();
        return athlete.UserID;
    }

    public static async Task<IReadOnlyList<PersonalRecord>> RecordsAsync(this RemodelDatabase database)
    {
        await using var context = database.CreateContext();
        return await context.PersonalRecords.AsNoTracking().ToListAsync();
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
    public static async Task<IReadOnlyList<string>> ChainsAsync(this RemodelDatabase database)
    {
        await using var context = database.CreateContext();
        var names = await context.MovementDatas
            .Select(movement => new
            {
                movement.MovementDataID,
                Name = movement.MovementBase.Name + " / " + movement.Equipment.Name
                       + (movement.MovementModifier == null ? string.Empty : " / " + movement.MovementModifier.Name),
            })
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
