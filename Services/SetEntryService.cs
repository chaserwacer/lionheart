using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services;

/// <summary>
/// Service for managing set entries within movements.
/// Handles business logic and ensures users can only access their own data.
/// </summary>
public class SetEntryService : ISetEntryService
{
    private readonly ModelContext _context;

    public SetEntryService(ModelContext context)
    {
        _context = context;
    }


    public async Task<Result<SetEntryDTO>> CreateSetEntryAsync(IdentityUser user, CreateSetEntryRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        
        // Verify user owns the movement
        var movement = await _context.Movements
            .Include(m => m.TrainingSession)
            .ThenInclude(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(m => m.MovementID == request.MovementID && 
                                    m.TrainingSession.TrainingProgram!.UserID == userGuid);

        if (movement == null)
        {
            return Result<SetEntryDTO>.NotFound("Movement not found or access denied.");
        }

        var setEntry = new SetEntry
        {
            SetEntryID = Guid.NewGuid(),
            MovementID = request.MovementID,
            Movement = movement,
            RecommendedReps = request.RecommendedReps,
            RecommendedWeight = request.RecommendedWeight,
            RecommendedRPE = request.RecommendedRPE,
            WeightUnit = request.WeightUnit,
            ActualReps = request.ActualReps,
            ActualWeight = request.ActualWeight,
            ActualRPE = request.ActualRPE
        };

        _context.SetEntries.Add(setEntry);
        await _context.SaveChangesAsync();
        return Result<SetEntryDTO>.Created(setEntry.ToDTO());
    }

    public async Task<Result<SetEntryDTO>> UpdateSetEntryAsync(IdentityUser user, UpdateSetEntryRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var setEntry = await _context.SetEntries
            .Include(se => se.Movement)
            .ThenInclude(m => m.TrainingSession)
            .ThenInclude(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(se => se.SetEntryID == request.SetEntryID);

        if (setEntry == null || setEntry.Movement.TrainingSession.TrainingProgram!.UserID != userGuid)
        {
            return Result<SetEntryDTO>.NotFound("Set entry not found or access denied.");
        }

        // Update only provided values
            setEntry.RecommendedReps = request.RecommendedReps;
        
            setEntry.RecommendedWeight = request.RecommendedWeight;
        
            setEntry.RecommendedRPE = request.RecommendedRPE;
        
            setEntry.WeightUnit = request.WeightUnit;
        
            setEntry.ActualReps = request.ActualReps;
        
            setEntry.ActualWeight = request.ActualWeight;
        
            setEntry.ActualRPE = request.ActualRPE;

        await _context.SaveChangesAsync();
        return Result<SetEntryDTO>.Success(setEntry.ToDTO());
    }

    public async Task<Result> DeleteSetEntryAsync(IdentityUser user, Guid setEntryId)
    {
        var userGuid = Guid.Parse(user.Id);
        var setEntry = await _context.SetEntries
            .Include(se => se.Movement)
            .ThenInclude(m => m.TrainingSession)
            .ThenInclude(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(se => se.SetEntryID == setEntryId);

        if (setEntry == null || setEntry.Movement.TrainingSession.TrainingProgram!.UserID != userGuid)
        {
            return Result.NotFound("Set entry not found or access denied.");
        }

        _context.SetEntries.Remove(setEntry);
        await _context.SaveChangesAsync();
        return Result.NoContent();
    }
}