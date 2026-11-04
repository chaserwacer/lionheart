namespace lionheart.Model.Health;

/// <summary>
/// An Apple Health workout whose imported session the athlete deleted, so it is never imported
/// again.
/// </summary>
/// <remarks>
/// Health still holds the workout, and the phone offers it again whenever it reads Health from the
/// start — after a sign-in on a fresh install, or when Lionheart starts taking a kind of workout it
/// left out before. Without this the deletion would last only until then.
/// </remarks>
public sealed class DeletedHealthWorkout
{
    private DeletedHealthWorkout() { }

    public Guid UserId { get; private init; }

    /// <summary>The workout's UUID in Health.</summary>
    public Guid WorkoutId { get; private init; }

    public static DeletedHealthWorkout Of(Guid userId, Guid workoutId) => new() { UserId = userId, WorkoutId = workoutId };
}
