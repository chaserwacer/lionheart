using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Training;
using lionheart.Model.Training.SetEntry;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services;

public interface IPersonalRecordService
{
    /// <summary>
    /// Gets all active personal records for a user.
    /// </summary>
    Task<Result<List<PersonalRecordDTO>>> GetPersonalRecordsAsync(IdentityUser user);

    /// <summary>
    /// Gets the PR summary for a specific MovementData.
    /// </summary>
    Task<Result<MovementDataPRSummary>> GetPRSummaryForMovementDataAsync(IdentityUser user, Guid movementDataId);

    /// <summary>
    /// Gets all PR summaries for all MovementDatas the user has PRs for.
    /// </summary>
    Task<Result<List<MovementDataPRSummary>>> GetAllPRSummariesAsync(IdentityUser user);

    /// <summary>
    /// Submit an attempt to check if it's a new PR.
    /// This should be called when a LiftSetEntry is created or updated,
    /// or when a TrainingSession is marked complete.
    /// </summary>
    Task<Result<PRAttemptResult>> SubmitAttemptAsync(IdentityUser user, LiftSetEntry setEntry, bool forceCheck = false);

    /// <summary>
    /// Process all lift sets in a training session for PR checking.
    /// Called when a TrainingSession is marked as Completed.
    /// </summary>
    Task<Result<List<PRAttemptResult>>> ProcessTrainingSessionAsync(IdentityUser user, Guid trainingSessionId);

    /// <summary>
    /// Reverts a PR to its previous state.
    /// Used when a set entry that was a PR is decreased.
    /// </summary>
    Task<Result<PersonalRecordDTO?>> RevertToPreviousAsync(IdentityUser user, Guid personalRecordId);

    /// <summary>
    /// Gets the history of PRs for a specific MovementData and type.
    /// </summary>
    Task<Result<List<PersonalRecordDTO>>> GetPRHistoryAsync(IdentityUser user, Guid movementDataId, PersonalRecordType prType);

    /// <summary>
    /// Checks if a set entry that was previously a PR needs to trigger a revert.
    /// Called when updating a set entry with decreased values.
    /// </summary>
    Task<Result> CheckAndRevertPRIfNeededAsync(IdentityUser user, LiftSetEntry setEntry, double oldWeight, int oldReps);
}

public class PersonalRecordService : IPersonalRecordService
{
    private readonly ModelContext _context;

    public PersonalRecordService(ModelContext context)
    {
        _context = context;
    }

    public async Task<Result<List<PersonalRecordDTO>>> GetPersonalRecordsAsync(IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);
        var records = await _context.PersonalRecords
            .Where(pr => pr.UserID == userGuid && pr.IsActive)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.Equipment)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.MovementBase)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.MovementModifier)
            .OrderByDescending(pr => pr.CreatedAt)
            .ToListAsync();

        return Result<List<PersonalRecordDTO>>.Success(records.Adapt<List<PersonalRecordDTO>>());
    }

    public async Task<Result<MovementDataPRSummary>> GetPRSummaryForMovementDataAsync(IdentityUser user, Guid movementDataId)
    {
        var userGuid = Guid.Parse(user.Id);

        var movementData = await _context.MovementDatas
            .Include(md => md.Equipment)
            .Include(md => md.MovementBase)
            .Include(md => md.MovementModifier)
            .FirstOrDefaultAsync(md => md.MovementDataID == movementDataId && md.UserID == userGuid);

        if (movementData == null)
        {
            return Result<MovementDataPRSummary>.NotFound("MovementData not found.");
        }

        var strengthPR = await _context.PersonalRecords
            .Where(pr => pr.UserID == userGuid && pr.MovementDataID == movementDataId && pr.PRType == PersonalRecordType.Strength && pr.IsActive)
            .Include(pr => pr.MovementData).ThenInclude(md => md.Equipment)
            .Include(pr => pr.MovementData).ThenInclude(md => md.MovementBase)
            .Include(pr => pr.MovementData).ThenInclude(md => md.MovementModifier)
            .FirstOrDefaultAsync();

        var volumePR = await _context.PersonalRecords
            .Where(pr => pr.UserID == userGuid && pr.MovementDataID == movementDataId && pr.PRType == PersonalRecordType.Volume && pr.IsActive)
            .Include(pr => pr.MovementData).ThenInclude(md => md.Equipment)
            .Include(pr => pr.MovementData).ThenInclude(md => md.MovementBase)
            .Include(pr => pr.MovementData).ThenInclude(md => md.MovementModifier)
            .FirstOrDefaultAsync();

        var lastPRDate = new[] { strengthPR?.CreatedAt, volumePR?.CreatedAt }
            .Where(d => d.HasValue)
            .Max();

        return Result<MovementDataPRSummary>.Success(new MovementDataPRSummary(
            movementDataId,
            movementData.Adapt<MovementDataDTO>(),
            strengthPR?.Adapt<PersonalRecordDTO>(),
            volumePR?.Adapt<PersonalRecordDTO>(),
            lastPRDate
        ));
    }

    public async Task<Result<List<MovementDataPRSummary>>> GetAllPRSummariesAsync(IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);

        // Get all MovementDatas that have active PRs
        var movementDataIds = await _context.PersonalRecords
            .Where(pr => pr.UserID == userGuid && pr.IsActive)
            .Select(pr => pr.MovementDataID)
            .Distinct()
            .ToListAsync();

        var summaries = new List<MovementDataPRSummary>();
        foreach (var mdId in movementDataIds)
        {
            var result = await GetPRSummaryForMovementDataAsync(user, mdId);
            if (result.IsSuccess)
            {
                summaries.Add(result.Value);
            }
        }

        return Result<List<MovementDataPRSummary>>.Success(summaries.OrderByDescending(s => s.LastPRDate).ToList());
    }

    public async Task<Result<PRAttemptResult>> SubmitAttemptAsync(IdentityUser user, LiftSetEntry setEntry, bool forceCheck = false)
    {
        var userGuid = Guid.Parse(user.Id);

        // Load the movement and its MovementData
        var movement = await _context.Movements
            .Include(m => m.MovementData)
                .ThenInclude(md => md.Equipment)
            .Include(m => m.MovementData)
                .ThenInclude(md => md.MovementBase)
            .Include(m => m.MovementData)
                .ThenInclude(md => md.MovementModifier)
            .Include(m => m.TrainingSession)
            .FirstOrDefaultAsync(m => m.MovementID == setEntry.MovementID);

        if (movement == null)
        {
            return Result<PRAttemptResult>.NotFound("Movement not found.");
        }

        // Check if we should process this attempt
        // Only process if forceCheck is true or if the session is completed
        if (!forceCheck && movement.TrainingSession.Status != TrainingSessionStatus.Completed)
        {
            return Result<PRAttemptResult>.Success(new PRAttemptResult(false, false, null, null, null, null));
        }

        var movementDataId = movement.MovementDataID;
        var weight = setEntry.ActualWeight;
        var reps = setEntry.ActualReps;
        var volume = weight * reps;

        // Get current active PRs for this MovementData
        var currentStrengthPR = await _context.PersonalRecords
            .Where(pr => pr.UserID == userGuid && pr.MovementDataID == movementDataId && pr.PRType == PersonalRecordType.Strength && pr.IsActive)
            .Include(pr => pr.MovementData).ThenInclude(md => md.Equipment)
            .Include(pr => pr.MovementData).ThenInclude(md => md.MovementBase)
            .Include(pr => pr.MovementData).ThenInclude(md => md.MovementModifier)
            .FirstOrDefaultAsync();

        var currentVolumePR = await _context.PersonalRecords
            .Where(pr => pr.UserID == userGuid && pr.MovementDataID == movementDataId && pr.PRType == PersonalRecordType.Volume && pr.IsActive)
            .Include(pr => pr.MovementData).ThenInclude(md => md.Equipment)
            .Include(pr => pr.MovementData).ThenInclude(md => md.MovementBase)
            .Include(pr => pr.MovementData).ThenInclude(md => md.MovementModifier)
            .FirstOrDefaultAsync();

        bool isNewStrengthPR = false;
        bool isNewVolumePR = false;
        PersonalRecord? newStrengthPR = null;
        PersonalRecord? newVolumePR = null;

        // Check for Strength PR (compare weight)
        if (currentStrengthPR == null || weight > currentStrengthPR.Weight)
        {
            isNewStrengthPR = true;

            // Deactivate old PR if exists
            if (currentStrengthPR != null)
            {
                currentStrengthPR.IsActive = false;
            }

            // Create new PR
            newStrengthPR = new PersonalRecord
            {
                PersonalRecordID = Guid.NewGuid(),
                UserID = userGuid,
                MovementDataID = movementDataId,
                MovementData = movement.MovementData,
                PRType = PersonalRecordType.Strength,
                Weight = weight,
                WeightUnit = setEntry.WeightUnit,
                Reps = reps,
                CreatedAt = DateTime.UtcNow,
                PreviousPRCreatedAt = currentStrengthPR?.CreatedAt,
                PreviousPersonalRecordID = currentStrengthPR?.PersonalRecordID,
                PreviousPersonalRecord = currentStrengthPR,
                SourceLiftSetEntryID = setEntry.SetEntryID,
                SourceLiftSetEntry = setEntry,
                IsActive = true
            };

            _context.PersonalRecords.Add(newStrengthPR);
        }

        // Check for Volume PR (compare weight * reps)
        if (currentVolumePR == null || volume > currentVolumePR.Volume)
        {
            isNewVolumePR = true;

            // Deactivate old PR if exists
            if (currentVolumePR != null)
            {
                currentVolumePR.IsActive = false;
            }

            // Create new PR
            newVolumePR = new PersonalRecord
            {
                PersonalRecordID = Guid.NewGuid(),
                UserID = userGuid,
                MovementDataID = movementDataId,
                MovementData = movement.MovementData,
                PRType = PersonalRecordType.Volume,
                Weight = weight,
                WeightUnit = setEntry.WeightUnit,
                Reps = reps,
                CreatedAt = DateTime.UtcNow,
                PreviousPRCreatedAt = currentVolumePR?.CreatedAt,
                PreviousPersonalRecordID = currentVolumePR?.PersonalRecordID,
                PreviousPersonalRecord = currentVolumePR,
                SourceLiftSetEntryID = setEntry.SetEntryID,
                SourceLiftSetEntry = setEntry,
                IsActive = true
            };

            _context.PersonalRecords.Add(newVolumePR);
        }


        if (isNewStrengthPR || isNewVolumePR)
        {
            await _context.SaveChangesAsync();
        }

        return Result<PRAttemptResult>.Success(new PRAttemptResult(
            isNewStrengthPR,
            isNewVolumePR,
            newStrengthPR?.Adapt<PersonalRecordDTO>(),
            newVolumePR?.Adapt<PersonalRecordDTO>(),
            currentStrengthPR?.Adapt<PersonalRecordDTO>(),
            currentVolumePR?.Adapt<PersonalRecordDTO>()
        ));
    }

    public async Task<Result<List<PRAttemptResult>>> ProcessTrainingSessionAsync(IdentityUser user, Guid trainingSessionId)
    {
        var userGuid = Guid.Parse(user.Id);

        var session = await _context.TrainingSessions
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.LiftSets)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.MovementData)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == trainingSessionId && ts.UserID == userGuid);

        if (session is null)
        {
            return Result<List<PRAttemptResult>>.NotFound("Training session not found.");
        }

        var results = new List<PRAttemptResult>();

        foreach (var movement in session.Movements)
        {
            foreach (var setEntry in movement.LiftSets)
            {
                var result = await SubmitAttemptAsync(user, setEntry, forceCheck: true);
                if (result.IsSuccess && (result.Value.IsNewStrengthPR || result.Value.IsNewVolumePR))
                {
                    results.Add(result.Value);
                }
            }
        }

        return Result<List<PRAttemptResult>>.Success(results);
    }

    public async Task<Result<PersonalRecordDTO?>> RevertToPreviousAsync(IdentityUser user, Guid personalRecordId)
    {
        var userGuid = Guid.Parse(user.Id);

        var pr = await _context.PersonalRecords
            .Include(pr => pr.PreviousPersonalRecord)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.Equipment)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.MovementBase)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.MovementModifier)
            .FirstOrDefaultAsync(pr => pr.PersonalRecordID == personalRecordId && pr.UserID == userGuid);

        if (pr == null)
        {
            return Result<PersonalRecordDTO?>.NotFound("Personal record not found.");
        }

        if (!pr.IsActive)
        {
            return Result<PersonalRecordDTO?>.Error("Cannot revert an inactive personal record.");
        }

        // Deactivate current PR
        pr.IsActive = false;

        // Reactivate previous PR if it exists
        if (pr.PreviousPersonalRecord != null)
        {
            pr.PreviousPersonalRecord.IsActive = true;
            await _context.SaveChangesAsync();
            return Result<PersonalRecordDTO?>.Success(pr.PreviousPersonalRecord.Adapt<PersonalRecordDTO>());
        }

        await _context.SaveChangesAsync();
        return Result<PersonalRecordDTO?>.Success(null);
    }

    public async Task<Result<List<PersonalRecordDTO>>> GetPRHistoryAsync(IdentityUser user, Guid movementDataId, PersonalRecordType prType)
    {
        var userGuid = Guid.Parse(user.Id);

        var history = await _context.PersonalRecords
            .Where(pr => pr.UserID == userGuid && pr.MovementDataID == movementDataId && pr.PRType == prType)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.Equipment)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.MovementBase)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.MovementModifier)
            .OrderByDescending(pr => pr.CreatedAt)
            .ToListAsync();

        return Result<List<PersonalRecordDTO>>.Success(history.Adapt<List<PersonalRecordDTO>>());
    }

    /// <summary>
    /// Checks if a set entry that was previously a PR needs to trigger a revert.
    /// Called when updating a set entry with decreased values.
    /// </summary>
    public async Task<Result> CheckAndRevertPRIfNeededAsync(IdentityUser user, LiftSetEntry setEntry, double oldWeight, int oldReps)
    {
        var userGuid = Guid.Parse(user.Id);

        // Find any PRs that reference this set entry
        var prsFromThisSet = await _context.PersonalRecords
            .Where(pr => pr.UserID == userGuid && pr.SourceLiftSetEntryID == setEntry.SetEntryID && pr.IsActive)
            .ToListAsync();

        foreach (var pr in prsFromThisSet)
        {
            bool shouldRevert = false;

            if (pr.PRType == PersonalRecordType.Strength && setEntry.ActualWeight < oldWeight)
            {
                shouldRevert = true;
            }
            else if (pr.PRType == PersonalRecordType.Volume && (setEntry.ActualWeight * setEntry.ActualReps) < (oldWeight * oldReps))
            {
                shouldRevert = true;
            }

            if (shouldRevert)
            {
                await RevertToPreviousAsync(user, pr.PersonalRecordID);
            }
        }

        return Result.Success();
    }
}
