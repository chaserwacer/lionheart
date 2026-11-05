using lionheart.Data;
using lionheart.Model.Health;
using lionheart.Model.Training.Sessions;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training.Sessions;

internal static class ImportedSessionRemoval
{
    extension(ModelContext context)
    {
        /// <summary>
        /// Stages what deleting a session imported from Apple Health or the Strava archive takes with
        /// it, without saving, so it lands in the same transaction as the delete: its recording goes,
        /// and its workout or activity is remembered as deleted, so no later import brings it back.
        /// </summary>
        /// <remarks>
        /// A recording carries no foreign key to its session (see <see cref="HealthConfiguration"/>),
        /// so nothing at the database level removes it, and a deleted run's route — where the athlete
        /// was — would otherwise outlive the session it belonged to.
        /// </remarks>
        public async Task StageImportedSessionRemovalAsync(TrainingSession session, CancellationToken ct)
        {
            if (!session.Source.IsRecording) return;

            var recording = await context.WorkoutRecordings
                .FirstOrDefaultAsync(held => held.TrainingSessionId == session.TrainingSessionId, ct);
            if (recording is not null) context.WorkoutRecordings.Remove(recording);

            switch (session.Source)
            {
                case SessionSource.AppleHealth health:
                    context.DeletedHealthWorkouts.Add(DeletedHealthWorkout.Of(session.UserId, health.WorkoutId));
                    break;
                case SessionSource.StravaArchive strava:
                    context.DeletedStravaActivities.Add(DeletedStravaActivity.Of(session.UserId, strava.ActivityId));
                    break;
                default:
                    throw new System.Diagnostics.UnreachableException("Only an imported recording is remembered as deleted.");
            }
        }
    }
}
