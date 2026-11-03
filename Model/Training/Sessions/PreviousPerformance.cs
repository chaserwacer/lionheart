using lionheart.Model.Training.Sets;

namespace lionheart.Model.Training.Sessions;

/// <summary>
/// The last time the athlete trained a movement: that session's sets of it, in the order performed.
/// </summary>
/// <remarks>
/// What a logging screen shows beside each set as "previous", and what the athlete trains to beat.
/// Warm-ups are included with their kind, because the screen lines sets up by position and a
/// warm-up holds a position; the client chooses which to compare against.
/// </remarks>
public sealed record PreviousPerformanceView(
    Guid MovementDataId,
    Guid TrainingSessionId,
    DateOnly Date,
    DateTimeOffset StartedAt,
    IReadOnlyList<SetEntry> Sets);

public static class PreviousPerformances
{
    /// <summary>
    /// One performance per movement any of these sessions trained, from the most recent session
    /// holding it by when it started.
    /// </summary>
    /// <remarks>
    /// Ordered by the absolute instant rather than the calendar day, so two sessions on one day
    /// still have a most recent, and one logged at another offset still ranks where it happened.
    /// </remarks>
    public static IReadOnlyList<PreviousPerformanceView> Of(IEnumerable<CompletedTrainingSession> sessions) =>
    [
        .. sessions
            .OrderByDescending(session => session.Period.StartTimeUtc)
            .SelectMany(session => session.Sets.ByMovement().Select(movement => new PreviousPerformanceView(
                movement.Key,
                session.TrainingSessionId,
                session.Date,
                session.Period.StartTime,
                [.. movement])))
            .DistinctBy(performance => performance.MovementDataId)
    ];
}
