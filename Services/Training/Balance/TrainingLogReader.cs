using lionheart.Data;
using lionheart.Services.Training.Movement;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;
using lionheart.Services.Training.Patterns;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Balance;

/// <summary>Reads the <see cref="TrainingLog"/> for an athlete.</summary>
/// <remarks>
/// The whole completed history up to a day, because last-trained days, priority bests and the
/// implied priority all look back further than any one window, and the history is a few hundred
/// sessions — small beside one month of heart rate.
/// </remarks>
public static class TrainingLogReader
{
    public static async Task<TrainingLog> ReadAsync(
        this ModelContext context, Guid userId, DateOnly through, MovementPatternMap map, CancellationToken ct)
    {
        var sessions = (await context.Sessions.OfType<CompletedTrainingSession>()
                .AsNoTracking()
                .Where(session => session.UserId == userId)
                .ToListAsync(ct))
            .Where(session => session.Date <= through)
            .OrderBy(session => session.Period.StartTime)
            .Select(session => new SessionInLog(
                session.TrainingSessionId,
                session.Date,
                [.. session.Sets.OfType<LiftSet>().Where(HardSets.IsHard)]))
            .ToList();

        var movements = await context.MovementDatas
            .AsNoTracking()
            .Where(movement => movement.UserID == userId)
            .Include(movement => movement.MovementBase)
            .Include(movement => movement.Equipment)
            .Include(movement => movement.MovementModifier)
            .ToListAsync(ct);

        var bodyweights = await context.BodyweightMeasurements
            .AsNoTracking()
            .Where(measured => measured.UserId == userId && measured.Day <= through)
            .OrderBy(measured => measured.Day)
            .ToListAsync(ct);

        return new TrainingLog(
            sessions,
            movements.ToDictionary(
                movement => movement.MovementDataID,
                movement => new LoggedMovement(
                    movement.MovementDataID,
                    MovementNames.Of(movement.MovementBase.Name, movement.Equipment.Name, movement.MovementModifier?.Name),
                    map.StandingOf(movement.MovementBase),
                    movement.Setup.AddsToBodyweight)),
            [.. bodyweights.Select(measured => new BodyweightOnDay(measured.Day, measured.Bodyweight))]);
    }
}
