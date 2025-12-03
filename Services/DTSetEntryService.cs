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
/// Service for managing distance/time set entries within movements.
/// Handles business logic and ensures users can only access their own data.
/// </summary>
[McpServerToolType]
public class DTSetEntryService : ISetEntryService
{
    private readonly ModelContext _context;

    public DTSetEntryService(ModelContext context)
    {
        _context = context;
    }

    [McpServerTool, Description("Add a distance/time set entry to a movement.")]
    public async Task<Result<ISetEntryDTO>> CreateSetEntryAsync(IdentityUser user, ICreateSetEntryRequest request)
    {
        if (request is not CreateDTSetEntryRequest dtRequest)
        {
            return Result<ISetEntryDTO>.Invalid(new List<ValidationError> 
            { 
                new() { ErrorMessage = "Request must be a CreateDTSetEntryRequest" } 
            });
        }

        var userGuid = Guid.Parse(user.Id);
        
        // Verify user owns the movement
        var movement = await _context.Movements
            .Include(m => m.TrainingSession)
            .ThenInclude(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(m => m.MovementID == dtRequest.MovementID && 
                                    m.TrainingSession.TrainingProgram!.UserID == userGuid);

        if (movement == null)
        {
            return Result<ISetEntryDTO>.NotFound("Movement not found or access denied.");
        }

        var setEntry = new DTSetEntry
        {
            SetEntryID = Guid.NewGuid(),
            MovementID = dtRequest.MovementID,
            Movement = movement,
            RecommendedDistance = dtRequest.RecommendedDistance,
            ActualDistance = dtRequest.ActualDistance,
            IntervalDuration = dtRequest.IntervalDuration,
            TargetPace = dtRequest.TargetPace,
            ActualPace = dtRequest.ActualPace,
            RecommendedDuration = dtRequest.RecommendedDuration,
            ActualDuration = dtRequest.ActualDuration,
            RecommendedRest = dtRequest.RecommendedRest,
            ActualRest = dtRequest.ActualRest,
            IntervalType = dtRequest.IntervalType,
            DistanceUnit = dtRequest.DistanceUnit,
            ActualRPE = dtRequest.ActualRPE
        };

        _context.SetEntries.Add(setEntry);
        await _context.SaveChangesAsync();
        
        return Result<ISetEntryDTO>.Created(setEntry.Adapt<DTSetEntryDTO>());
    }

    [McpServerTool, Description("Update an existing distance/time set entry.")]
    public async Task<Result<ISetEntryDTO>> UpdateSetEntryAsync(IdentityUser user, IUpdateSetEntryRequest request)
    {
        if (request is not UpdateDTSetEntryRequest dtRequest)
        {
            return Result<ISetEntryDTO>.Invalid(new List<ValidationError> 
            { 
                new() { ErrorMessage = "Request must be an UpdateDTSetEntryRequest" } 
            });
        }

        var userGuid = Guid.Parse(user.Id);
        var setEntry = await _context.SetEntries
            .OfType<DTSetEntry>()
            .Include(se => se.Movement)
            .ThenInclude(m => m.TrainingSession)
            .ThenInclude(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(se => se.SetEntryID == dtRequest.SetEntryID);

        if (setEntry == null || setEntry.Movement.TrainingSession.TrainingProgram!.UserID != userGuid)
        {
            return Result<ISetEntryDTO>.NotFound("Set entry not found or access denied.");
        }

        // Update values
        setEntry.RecommendedDistance = dtRequest.RecommendedDistance;
        setEntry.ActualDistance = dtRequest.ActualDistance;
        setEntry.IntervalDuration = dtRequest.IntervalDuration;
        setEntry.TargetPace = dtRequest.TargetPace;
        setEntry.ActualPace = dtRequest.ActualPace;
        setEntry.RecommendedDuration = dtRequest.RecommendedDuration;
        setEntry.ActualDuration = dtRequest.ActualDuration;
        setEntry.RecommendedRest = dtRequest.RecommendedRest;
        setEntry.ActualRest = dtRequest.ActualRest;
        setEntry.IntervalType = dtRequest.IntervalType;
        setEntry.DistanceUnit = dtRequest.DistanceUnit;
        setEntry.ActualRPE = dtRequest.ActualRPE;

        await _context.SaveChangesAsync();
        
        return Result<ISetEntryDTO>.Success(setEntry.Adapt<DTSetEntryDTO>());
    }

    [McpServerTool, Description("Delete a distance/time set entry.")]
    public async Task<Result> DeleteSetEntryAsync(IdentityUser user, Guid setEntryId)
    {
        var userGuid = Guid.Parse(user.Id);
        var setEntry = await _context.SetEntries
            .OfType<DTSetEntry>()
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
