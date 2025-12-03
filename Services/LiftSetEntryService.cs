using System.ComponentModel;
using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using lionheart.Model.TrainingProgram.SetEntry;
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
public class LiftSetEntryService : ISetEntryService
{
    private readonly ModelContext _context;

    public LiftSetEntryService(ModelContext context)
    {
        _context = context;
    }

    [McpServerTool, Description("Add a lifting set entry to a movement.")]
    public async Task<Result<ISetEntryDTO>> CreateSetEntryAsync(IdentityUser user, ICreateSetEntryRequest request)
    {
        if (request is not CreateLiftSetEntryRequest liftRequest)
        {
            return Result<ISetEntryDTO>.Invalid(new List<ValidationError> 
            { 
                new() { ErrorMessage = "Request must be a CreateLiftSetEntryRequest" } 
            });
        }

        var userGuid = Guid.Parse(user.Id);
        
        // Verify user owns the movement
        var movement = await _context.Movements
            .Include(m => m.TrainingSession)
            .ThenInclude(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(m => m.MovementID == liftRequest.MovementID && 
                                    m.TrainingSession.TrainingProgram!.UserID == userGuid);

        if (movement == null)
        {
            return Result<ISetEntryDTO>.NotFound("Movement not found or access denied.");
        }

        var setEntry = new LiftSetEntry
        {
            SetEntryID = Guid.NewGuid(),
            MovementID = liftRequest.MovementID,
            Movement = movement,
            RecommendedReps = liftRequest.RecommendedReps,
            RecommendedWeight = liftRequest.RecommendedWeight,
            RecommendedRPE = liftRequest.RecommendedRPE,
            ActualReps = liftRequest.ActualReps,
            ActualWeight = liftRequest.ActualWeight,
            ActualRPE = liftRequest.ActualRPE,
            WeightUnit = liftRequest.WeightUnit
        };

        _context.SetEntries.Add(setEntry);
        await _context.SaveChangesAsync();
        
        return Result<ISetEntryDTO>.Created(setEntry.Adapt<LiftSetEntryDTO>());
    }

    [McpServerTool, Description("Update an existing lifting set entry.")]
    public async Task<Result<ISetEntryDTO>> UpdateSetEntryAsync(IdentityUser user, IUpdateSetEntryRequest request)
    {
        if (request is not UpdateLiftSetEntryRequest liftRequest)
        {
            return Result<ISetEntryDTO>.Invalid(new List<ValidationError> 
            { 
                new() { ErrorMessage = "Request must be an UpdateLiftSetEntryRequest" } 
            });
        }

        var userGuid = Guid.Parse(user.Id);
        var setEntry = await _context.SetEntries
            .OfType<LiftSetEntry>()
            .Include(se => se.Movement)
            .ThenInclude(m => m.TrainingSession)
            .ThenInclude(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(se => se.SetEntryID == liftRequest.SetEntryID);

        if (setEntry == null || setEntry.Movement.TrainingSession.TrainingProgram!.UserID != userGuid)
        {
            return Result<ISetEntryDTO>.NotFound("Set entry not found or access denied.");
        }

        // Update values
        setEntry.RecommendedReps = liftRequest.RecommendedReps;
        setEntry.RecommendedWeight = liftRequest.RecommendedWeight;
        setEntry.RecommendedRPE = liftRequest.RecommendedRPE;
        setEntry.ActualReps = liftRequest.ActualReps;
        setEntry.ActualWeight = liftRequest.ActualWeight;
        setEntry.ActualRPE = liftRequest.ActualRPE;
        setEntry.WeightUnit = liftRequest.WeightUnit;

        await _context.SaveChangesAsync();
        
        return Result<ISetEntryDTO>.Success(setEntry.Adapt<LiftSetEntryDTO>());
    }

    [McpServerTool, Description("Delete a lifting set entry.")]
    public async Task<Result> DeleteSetEntryAsync(IdentityUser user, Guid setEntryId)
    {
        var userGuid = Guid.Parse(user.Id);
        var setEntry = await _context.SetEntries
            .OfType<LiftSetEntry>()
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
