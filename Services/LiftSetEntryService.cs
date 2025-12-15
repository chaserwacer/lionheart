using System.ComponentModel;
using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Training;
using lionheart.Model.Training.SetEntry;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Mapster;

namespace lionheart.Services;

/// <summary>
/// Service for managing lifting set entries within movements.
/// Handles business logic and ensures users can only access their own data.
/// </summary>
[McpServerToolType]
public class LiftSetEntryService : ILiftSetEntryService
{
    private readonly ModelContext _context;

    public LiftSetEntryService(ModelContext context)
    {
        _context = context;
    }

    [McpServerTool, Description("Add a lifting set entry to a movement.")]
    public async Task<Result<LiftSetEntryDTO>> CreateLiftSetEntryAsync(IdentityUser user, CreateLiftSetEntryRequest request)
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
        
        return Result<LiftSetEntryDTO>.Created(setEntry.Adapt<LiftSetEntryDTO>());
    }

    [McpServerTool, Description("Update an existing lifting set entry.")]
    public async Task<Result<LiftSetEntryDTO>> UpdateLiftSetEntryAsync(IdentityUser user, UpdateLiftSetEntryRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var setEntry = await _context.LiftSetEntries
            .Include(se => se.Movement)
            .ThenInclude(m => m.TrainingSession)
            .ThenInclude(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(se => se.SetEntryID == request.SetEntryID);

        if (setEntry == null || setEntry.Movement.TrainingSession.TrainingProgram!.UserID != userGuid)
        {
            return Result<LiftSetEntryDTO>.NotFound("Set entry not found or access denied.");
        }

        // Update values
        setEntry.RecommendedReps = request.RecommendedReps;
        setEntry.RecommendedWeight = request.RecommendedWeight;
        setEntry.RecommendedRPE = request.RecommendedRPE;
        setEntry.ActualReps = request.ActualReps;
        setEntry.ActualWeight = request.ActualWeight;
        setEntry.ActualRPE = request.ActualRPE;
        setEntry.WeightUnit = request.WeightUnit;

        await _context.SaveChangesAsync();
        
        return Result<LiftSetEntryDTO>.Success(setEntry.Adapt<LiftSetEntryDTO>());
    }

    [McpServerTool, Description("Delete a lifting set entry.")]
    public async Task<Result> DeleteLiftSetEntryAsync(IdentityUser user, Guid setEntryId)
    {
        var userGuid = Guid.Parse(user.Id);
        var setEntry = await _context.LiftSetEntries
            .Include(se => se.Movement)
            .ThenInclude(m => m.TrainingSession)
            .ThenInclude(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(se => se.SetEntryID == setEntryId);

        if (setEntry == null || setEntry.Movement.TrainingSession.TrainingProgram!.UserID != userGuid)
        {
            return Result.NotFound("Set entry not found or access denied.");
        }

        _context.LiftSetEntries.Remove(setEntry);
        await _context.SaveChangesAsync();
        return Result.NoContent();
    }
}
