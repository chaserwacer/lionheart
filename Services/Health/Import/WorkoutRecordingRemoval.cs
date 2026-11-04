using lionheart.Data;
using lionheart.Model.Health;
using lionheart.Model.Training.Sessions;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Health.Import;

internal static class WorkoutRecordingRemoval
{
    extension(ModelContext context)
    {
        /// <summary>
        /// Stages what deleting a session imported from Apple Health takes with it, without saving,
        /// so it lands in the same transaction as the delete: its recording goes, and its workout is
        /// remembered as deleted, so no later read of Health imports it again.
        /// </summary>
        /// <remarks>
        /// A recording carries no foreign key to its session (see <see cref="HealthConfiguration"/>),
        /// so nothing at the database level removes it, and a deleted run's route — where the athlete
        /// was — would otherwise outlive the session it belonged to.
        /// </remarks>
        public async Task StageRecordingRemovalAsync(TrainingSession session, CancellationToken ct)
        {
            if (session.Source is not SessionSource.AppleHealth health) return;

            var recording = await context.WorkoutRecordings
                .FirstOrDefaultAsync(held => held.TrainingSessionId == session.TrainingSessionId, ct);
            if (recording is not null) context.WorkoutRecordings.Remove(recording);

            context.DeletedHealthWorkouts.Add(DeletedHealthWorkout.Of(session.UserId, health.WorkoutId));
        }
    }
}
