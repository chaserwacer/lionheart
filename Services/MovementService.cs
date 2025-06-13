using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services;

/// <summary>
/// Service for managing movements within training sessions.
/// Handles business logic and ensures users can only access their own data.
/// </summary>
public class MovementService : IMovementService
{
    private readonly ModelContext _context;

    public MovementService(ModelContext context)
    {
        _context = context;
    }

    public async Task<Result<List<Movement>>> GetMovementsAsync(IdentityUser user, Guid trainingSessionID)
    {
        var userGuid = Guid.Parse(user.Id);
        
        // Verify user owns the training session
        var session = await _context.TrainingSessions
            .Include(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == trainingSessionID && 
                                     ts.TrainingProgram!.UserID == userGuid);
        
        if (session is null)
        {
            return Result<List<Movement>>.NotFound("Training session not found or access denied.");
        }

        var movements = await _context.Movements
            .Where(m => m.TrainingSessionID == trainingSessionID)
            .Include(m => m.MovementBase)
            .Include(m => m.Sets)
            .ToListAsync();

        return Result<List<Movement>>.Success(movements);
    }


    public async Task<Result<Movement>> CreateMovementAsync(IdentityUser user, CreateMovementRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        
        // Verify user owns the training session
        var session = await _context.TrainingSessions
            .Include(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == request.TrainingSessionID && 
                                     ts.TrainingProgram!.UserID == userGuid);
        
        if (session is null)
        {
            return Result<Movement>.NotFound("Training session not found or access denied.");
        }

        // Verify movement base exists
        var movementBase = await _context.MovementBases.FindAsync(request.MovementBaseID);  
        
        if (movementBase is null)
        {
            return Result<Movement>.NotFound("Movement base not found.");
        }

        var movement = new Movement
        {
            MovementID = Guid.NewGuid(),
            TrainingSessionID = request.TrainingSessionID,
            MovementBaseID = request.MovementBaseID,
            Notes = request.Notes,
            MovementModifier = request.MovementModifier,
            IsCompleted = false
        };

        _context.Movements.Add(movement);
        await _context.SaveChangesAsync();
        return Result<Movement>.Created(movement);
    }

    public async Task<Result<Movement>> UpdateMovementAsync(IdentityUser user, UpdateMovementRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var movement = await _context.Movements
            .Include(m => m.TrainingSession)
            .ThenInclude(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(m => m.MovementID == request.MovementID);

        if (movement == null)
        {
            return Result<Movement>.NotFound("Movement not found.");
        }
        else if (movement.TrainingSession.TrainingProgram!.UserID != userGuid)
        {
            return Result<Movement>.Unauthorized("You do not have permission to update this movement.");
        }

        // Verify movement base exists if being updated
        var movementBase = await _context.MovementBases.FindAsync(request.MovementBaseID);    
        
        if (movementBase == null)
        {
            return Result<Movement>.NotFound("Movement base not found.");
        }

        movement.MovementBaseID = request.MovementBaseID;
        movement.Notes = request.Notes;
        movement.MovementModifier = request.MovementModifier;
        movement.IsCompleted = request.IsCompleted;

        await _context.SaveChangesAsync();
        return Result<Movement>.Success(movement);
    }

    public async Task<Result> DeleteMovementAsync(IdentityUser user, Guid movementId)
    {
        var userGuid = Guid.Parse(user.Id);
        var movement = await _context.Movements
            .Include(m => m.TrainingSession)
            .ThenInclude(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(m => m.MovementID == movementId);

        if (movement == null)
        {
            return Result.NotFound("Movement not found.");
        }
        else if (movement.TrainingSession.TrainingProgram!.UserID != userGuid)
        {
            return Result.Unauthorized("You do not have permission to update this movement.");
        }

        _context.Movements.Remove(movement);
        await _context.SaveChangesAsync();
        return Result.NoContent();
    }

    public async Task<Result<List<MovementBase>>> GetMovementBasesAsync()
    {
        var movementBases = await _context.MovementBases
            .OrderBy(mb => mb.Name)
            .ToListAsync();

        return Result<List<MovementBase>>.Success(movementBases);
    }

    public async Task<Result<MovementBase>> CreateMovementBaseAsync(CreateMovementBaseRequest request)
    {
        // Check if movement base with this name already exists
        var existingBase = await _context.MovementBases
            .FirstOrDefaultAsync(mb => mb.Name.ToLower() == request.Name.ToLower());
        
        if (existingBase != null)
        {
            return Result<MovementBase>.Conflict("A movement base with this name already exists.");
        }

        var movementBase = new MovementBase
        {
            MovementBaseID = Guid.NewGuid(),
            Name = request.Name
        };

        _context.MovementBases.Add(movementBase);
        await _context.SaveChangesAsync();
        return Result<MovementBase>.Created(movementBase);
    }

    public async Task<Result> UpdateMovementsCompletion(IdentityUser user, UpdateMovementsCompletionRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        
        // Verify user owns the training session
        var session = await _context.TrainingSessions
            .Include(ts => ts.TrainingProgram)
            .Include(ts => ts.Movements)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == request.TrainingSessionID &&
                           ts.TrainingProgram!.UserID == userGuid);
        
        if (session is null)
        {
            return Result.NotFound("Training session not found or access denied.");
        }

        // Update completion status for each movement
        foreach (var movement in session.Movements)
        {
            movement.IsCompleted = request.Complete;
        }

        await _context.SaveChangesAsync();
        return Result.Success();
    }
}