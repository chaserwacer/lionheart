using lionheart.Data;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Health;

internal static class WorkoutRecordingRemoval
{
    extension(ModelContext context)
    {
        /// <summary>
        /// Stages the removal of a session's recording, if it has one, without saving — so it lands
        /// in the same transaction as the delete that caused it.
        /// </summary>
        /// <remarks>
        /// A recording carries no foreign key to its session (see <see cref="HealthConfiguration"/>),
        /// so nothing at the database level removes it, and a deleted run's route — where the athlete
        /// was — would otherwise outlive the session it belonged to.
        /// </remarks>
        public async Task StageRecordingRemovalAsync(Guid trainingSessionId, CancellationToken ct)
        {
            var recording = await context.WorkoutRecordings
                .FirstOrDefaultAsync(held => held.TrainingSessionId == trainingSessionId, ct);

            if (recording is not null) context.WorkoutRecordings.Remove(recording);
        }
    }
}
