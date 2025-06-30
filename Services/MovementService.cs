using System.ComponentModel;
using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

[McpServerToolType]
public class MovementService : IMovementService
{
    private readonly ModelContext _context;

    public MovementService(ModelContext context)
    {
        _context = context;
    }

    [McpServerTool, Description("Get all movements for a specific training session.")]
    public async Task<Result<List<MovementDTO>>> GetMovementsAsync(IdentityUser user, Guid sessionId)
    {
        var userGuid = Guid.Parse(user.Id);

        // Verify user owns the training session
        var session = await _context.TrainingSessions
            .Include(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == sessionId &&
                                     ts.TrainingProgram!.UserID == userGuid);

        if (session is null)
        {
            return Result<List<MovementDTO>>.NotFound("Training session not found or access denied.");
        }

        var movements = await _context.Movements
            .Where(m => m.TrainingSessionID == sessionId)
            .Include(m => m.MovementBase)
            .Include(m => m.Sets)
            .OrderBy(m => m.Ordering)
            .ToListAsync();

        return Result<List<MovementDTO>>.Success(movements.Select(m => m.ToDTO()).ToList());
    }


    [McpServerTool, Description("Create a new movement within a training session.")]
    public async Task<Result<MovementDTO>> CreateMovementAsync(IdentityUser user, CreateMovementRequest request)
    {
        var userGuid = Guid.Parse(user.Id);

        // Verify user owns the training session
        var session = await _context.TrainingSessions
            .Include(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == request.TrainingSessionID &&
                                     ts.TrainingProgram!.UserID == userGuid);

        if (session is null)
        {
            return Result<MovementDTO>.NotFound("Training session not found or access denied.");
        }

        // Verify movement base exists
        var movementBase = await _context.MovementBases.FindAsync(request.MovementBaseID);

        if (movementBase is null)
        {
            return Result<MovementDTO>.NotFound("Movement base not found.");
        }

        var orderings = await _context.Movements.Select(m => (int?)m.Ordering).ToListAsync();
        var maxOrdering = orderings.Count > 0 && orderings.Any(o => o.HasValue) ? orderings.Max() ?? -1 : -1;
        var movement = new Movement
        {
            MovementID = Guid.NewGuid(),
            TrainingSessionID = request.TrainingSessionID,
            MovementBaseID = request.MovementBaseID,
            Notes = request.Notes,
            MovementModifier = request.MovementModifier,
            IsCompleted = false,
            MovementBase = movementBase,
            Ordering = maxOrdering + 1,
            WeightUnit = request.WeightUnit
        };
        var x = movement.MovementBaseID;
        _context.Movements.Add(movement);
        await _context.SaveChangesAsync();

        return Result<MovementDTO>.Created(movement.ToDTO());
    }

    [McpServerTool, Description("Update an existing movement.")]
    public async Task<Result<MovementDTO>> UpdateMovementAsync(IdentityUser user, UpdateMovementRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var movement = await _context.Movements
            .Include(m => m.TrainingSession)
            .ThenInclude(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(m => m.MovementID == request.MovementID);



        if (movement == null)
        {
            return Result<MovementDTO>.NotFound("Movement not found.");
        }
        else if (movement.TrainingSession.TrainingProgram!.UserID != userGuid)
        {
            return Result<MovementDTO>.Unauthorized("You do not have permission to update this movement.");
        }

        // Verify movement base exists if being updated
        var movementBase = await _context.MovementBases.FindAsync(request.MovementBaseID);

        if (movementBase == null)
        {
            return Result<MovementDTO>.NotFound("Movement base not found.");
        }

        movement.MovementBaseID = request.MovementBaseID;
        movement.Notes = request.Notes;
        movement.MovementModifier = request.MovementModifier;
        movement.IsCompleted = request.IsCompleted;
        movement.WeightUnit = request.WeightUnit;

        await _context.SaveChangesAsync();
        return Result<MovementDTO>.Success(movement.ToDTO());
    }

    [McpServerTool, Description("Delete a movement.")]
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

    [McpServerTool, Description("Gets all movement bases available for creating movements.")]
    public async Task<Result<List<MovementBase>>> GetMovementBasesAsync()
    {
        var movementBases = await _context.MovementBases
            .OrderBy(mb => mb.Name)
            .ToListAsync();

        return Result<List<MovementBase>>.Success(movementBases);
    }

    [McpServerTool, Description("Create a new movement base.")]
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

    [McpServerTool, Description("Update completion status of all movements in a session.")]
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

    [McpServerTool, Description("Reorder movements in a training session.")]
    public async Task<Result> UpdateMovementOrder(IdentityUser user, UpdateMovementOrderRequest request)
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

        // Get all movements for this session
        var sessionMovementIds = session.Movements.Select(m => m.MovementID).ToHashSet();
        var requestMovementIds = request.IDs.ToHashSet();

        // Validate that the IDs in the request match exactly with the movements in the session
        if (!sessionMovementIds.SetEquals(requestMovementIds))
        {
            return Result.Invalid(new List<ValidationError> {
            new ValidationError { ErrorMessage = "Movement IDs don't match exactly with session movements." }
            });
        }

        // Update movement order based on the sequence in request.IDs
        for (int i = 0; i < request.IDs.Count; i++)
        {
            var movement = session.Movements.First(m => m.MovementID == request.IDs[i]);
            movement.Ordering = i;
        }

        await _context.SaveChangesAsync();
        return Result.Success();
    }
    
    [McpServerTool, Description("Delete a movement base.")]
    public async Task<Result> DeleteMovementBaseAsync(IdentityUser user, Guid movementBaseId)
    {
        // Verify the base exists
        var movementBase = await _context.MovementBases.FindAsync(movementBaseId);
        if (movementBase == null)
        {
            return Result.NotFound("Movement base not found.");
        }

        // Prevent deleting a base that’s in use
        var inUse = await _context.Movements.AnyAsync(m => m.MovementBaseID == movementBaseId);
        if (inUse)
        {
            return Result.Conflict("Cannot delete movement base while it has associated movements.");
        }

        // Remove and save
        _context.MovementBases.Remove(movementBase);
        await _context.SaveChangesAsync();
        return Result.NoContent();
    }

}