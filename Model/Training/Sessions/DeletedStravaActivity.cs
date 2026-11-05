namespace lionheart.Model.Training.Sessions;

/// <summary>
/// A Strava activity whose session, imported from the archive, the athlete deleted, so no later run
/// of the import brings it back.
/// </summary>
/// <remarks>
/// The archive still holds the activity, and the next archive Strava exports will too; without this
/// a deletion would last only until the import ran again.
/// </remarks>
public sealed class DeletedStravaActivity
{
    private DeletedStravaActivity() { }

    public Guid UserId { get; private init; }

    /// <summary>The activity's id in Strava.</summary>
    public long ActivityId { get; private init; }

    public static DeletedStravaActivity Of(Guid userId, long activityId) => new() { UserId = userId, ActivityId = activityId };
}
