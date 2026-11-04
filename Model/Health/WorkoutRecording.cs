namespace lionheart.Model.Health;

/// <summary>
/// What the app that recorded a workout captured beyond the session it became: its route and its
/// own heart rate.
/// </summary>
/// <remarks>
/// Attached to the completed session by the session's id rather than folded into it, for two
/// reasons. Size: an hour's route is thousands of fixes, and every read of the training log would
/// otherwise carry them. Privacy: the route is where the athlete was, and keeping it in its own
/// table means no read of the log — and so nothing the chat model is handed — can reach it by
/// accident. Everything else about the run (distance, time, climb) is the session's own set,
/// which is what records, the cardio report and the score already read.
/// <para>
/// Written once, at import, and never changed: a correction to the session restates its set, and
/// the route stays what the device recorded.
/// </para>
/// </remarks>
public sealed class WorkoutRecording
{
    private WorkoutRecording() { }

    public Guid TrainingSessionId { get; private init; }
    public Guid UserId { get; private init; }
    public WorkoutRoute Route { get; private init; } = WorkoutRoute.None;
    public RecordedHeartRate HeartRate { get; private init; } = RecordedHeartRate.None;

    public static WorkoutRecording Of(Guid trainingSessionId, Guid userId, WorkoutRoute route, RecordedHeartRate heartRate) =>
        new()
        {
            TrainingSessionId = trainingSessionId,
            UserId = userId,
            Route = route,
            HeartRate = heartRate,
        };
}
