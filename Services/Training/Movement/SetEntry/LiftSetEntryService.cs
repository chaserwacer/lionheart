using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Training;
using lionheart.Model.Training.SetEntry;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services
{
    /// <summary>
    /// Service for managing lifting set entries within movements.
    /// Handles business logic and ensures users can only access their own data.
    /// Integrates with PersonalRecordService for PR tracking.
    /// </summary>
    public class LiftSetEntryService : ILiftSetEntryService
    {
        private readonly ModelContext _context;
        private readonly IPersonalRecordService _personalRecordService;

        public LiftSetEntryService(ModelContext context, IPersonalRecordService personalRecordService)
        {
            _context = context;
            _personalRecordService = personalRecordService;
        }

        public async Task<Result<LiftSetEntryDTO>> CreateLiftSetEntryAsync(IdentityUser user, CreateLiftSetEntryRequest request)
        {
            var userGuid = Guid.Parse(user.Id);

            var movement = await _context.Movements
                .Include(m => m.TrainingSession)
                .FirstOrDefaultAsync(m => m.MovementID == request.MovementID && m.TrainingSession.UserID == userGuid);

            if (movement is null)
            {
                return Result<LiftSetEntryDTO>.NotFound("Movement not found or access denied.");
            }

            var setEntry = new LiftSetEntry
            {
                SetEntryID = Guid.NewGuid(),
                MovementID = request.MovementID,
                Movement = movement,
                RecommendedReps = request.RecommendedReps,
                RecommendedWeight = request.RecommendedWeight,
                RecommendedRPE = request.RecommendedRPE,
                ActualReps = request.ActualReps,
                ActualWeight = request.ActualWeight,
                ActualRPE = request.ActualRPE,
                WeightUnit = request.WeightUnit
            };

            _context.LiftSetEntries.Add(setEntry);
            await _context.SaveChangesAsync();

            // Check for PRs if the training session is already completed
            if (movement.TrainingSession.Status == TrainingSessionStatus.Completed)
            {
                await _personalRecordService.SubmitAttemptAsync(user, setEntry, forceCheck: true);
            }

            return Result<LiftSetEntryDTO>.Created(setEntry.ToDTO());
        }

        public async Task<Result<LiftSetEntryDTO>> UpdateLiftSetEntryAsync(IdentityUser user, UpdateLiftSetEntryRequest request)
        {
            var userGuid = Guid.Parse(user.Id);
            var setEntry = await _context.LiftSetEntries
                .Include(se => se.Movement)
                .ThenInclude(m => m.TrainingSession)
                .FirstOrDefaultAsync(se => se.SetEntryID == request.SetEntryID);

            if (setEntry is null || setEntry.Movement.TrainingSession.UserID != userGuid)
            {
                return Result<LiftSetEntryDTO>.NotFound("Set entry not found or access denied.");
            }

            // Store old values for PR revert check
            var oldWeight = setEntry.ActualWeight;
            var oldReps = setEntry.ActualReps;
            var isSessionCompleted = setEntry.Movement.TrainingSession.Status == TrainingSessionStatus.Completed;

            // Update values
            setEntry.RecommendedReps = request.RecommendedReps;
            setEntry.RecommendedWeight = request.RecommendedWeight;
            setEntry.RecommendedRPE = request.RecommendedRPE;
            setEntry.ActualReps = request.ActualReps;
            setEntry.ActualWeight = request.ActualWeight;
            setEntry.ActualRPE = request.ActualRPE;
            setEntry.WeightUnit = request.WeightUnit;

            await _context.SaveChangesAsync();

            // Handle PR checking for completed sessions
            if (isSessionCompleted)
            {
                // Check if values decreased - if so, check if we need to revert any PRs
                if (request.ActualWeight < oldWeight || (request.ActualWeight * request.ActualReps) < (oldWeight * oldReps))
                {
                    await _personalRecordService.CheckAndRevertPRIfNeededAsync(user, setEntry, oldWeight, oldReps);
                }

                // Check for new PRs with updated values
                await _personalRecordService.SubmitAttemptAsync(user, setEntry, forceCheck: true);
            }

            return Result<LiftSetEntryDTO>.Success(setEntry.ToDTO());
        }

        public async Task<Result> DeleteLiftSetEntryAsync(IdentityUser user, Guid setEntryId)
        {
            var userGuid = Guid.Parse(user.Id);
            var setEntry = await _context.LiftSetEntries
                .Include(se => se.Movement)
                .ThenInclude(m => m.TrainingSession)
                .FirstOrDefaultAsync(se => se.SetEntryID == setEntryId);

            if (setEntry is null || setEntry.Movement.TrainingSession.UserID != userGuid)
            {
                return Result.NotFound("Set entry not found or access denied.");
            }

            _context.LiftSetEntries.Remove(setEntry);
            await _context.SaveChangesAsync();
            return Result.NoContent();
        }
    }
}
