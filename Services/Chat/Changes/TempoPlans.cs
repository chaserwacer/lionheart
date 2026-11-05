using lionheart.Data;
using lionheart.Functional;
using lionheart.Model.Training.Sessions;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Chat.Changes;

/// <summary>The planned sessions the chat may change or remove: the ones it planned.</summary>
/// <remarks>
/// Asked twice — when a change is drafted, so the model hears a refusal it can act on, and again
/// when it is confirmed, because the session may have been started, completed or changed on Train
/// in between, and deleting a session that has since been trained would delete the training.
/// </remarks>
internal static class TempoPlans
{
    extension(ModelContext context)
    {
        public async Task<Result<PlannedTrainingSession, Problem>> PlannedByTempoAsync(
            Guid userId, Guid trainingSessionId, CancellationToken ct)
        {
            var session = await context.Sessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.TrainingSessionId == trainingSessionId && s.UserId == userId, ct);

            return session switch
            {
                null => Result<PlannedTrainingSession, Problem>.Failure(
                    new Problem.NotFound("No session of his has that id; GetPlannedDays lists the planned ones.")),
                PlannedTrainingSession { Provenance: Provenance.AiAssisted } planned =>
                    Result<PlannedTrainingSession, Problem>.Success(planned),
                PlannedTrainingSession => Result<PlannedTrainingSession, Problem>.Failure(new Problem.Forbidden(
                    "He planned that session himself, and Tempo changes only the sessions it planned. He can change it on Train.")),
                _ => Result<PlannedTrainingSession, Problem>.Failure(new Problem.Conflict(
                    "That session is no longer planned: it has been started, completed or skipped.")),
            };
        }
    }
}
