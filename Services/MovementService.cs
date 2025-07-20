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
            .Include(m => m.MovementModifier.Equipment)
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

        var equipment = await _context.Equipments.FindAsync(request.MovementModifier.EquipmentID);
        if (equipment == null)
        {
            return Result<MovementDTO>.NotFound("Equipment not found.");
        }

        var orderings = await _context.Movements.Select(m => (int?)m.Ordering).ToListAsync();
        var maxOrdering = orderings.Count > 0 && orderings.Any(o => o.HasValue) ? orderings.Max() ?? -1 : -1;
        var movement = new Movement
        {
            MovementID = Guid.NewGuid(),
            TrainingSessionID = request.TrainingSessionID,
            MovementBaseID = request.MovementBaseID,
            Notes = request.Notes,
            MovementModifier = new MovementModifier
            {
                Name = request.MovementModifier.Name,
                EquipmentID = equipment.EquipmentID,
                Equipment = equipment,
                Duration = request.MovementModifier.Duration
            },
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

        var equipment = await _context.Equipments.FindAsync(request.MovementModifier.EquipmentID);
        if (equipment == null)
        {
            return Result<MovementDTO>.NotFound("Equipment not found.");
        }
    
        movement.MovementBaseID = request.MovementBaseID;
        movement.Notes = request.Notes;
        movement.MovementModifier = new MovementModifier
        {
            Name = request.MovementModifier.Name,
            EquipmentID = equipment.EquipmentID,
            Equipment = equipment,
            Duration = request.MovementModifier.Duration
        };
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

    [McpServerTool, Description("Gets all movement bases available for creating movements for a user.")]
    public async Task<Result<List<MovementBase>>> GetMovementBasesAsync(IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);
        var movementBases = await _context.MovementBases
            .Where(mb => mb.UserID == userGuid) // include global/shared
            .OrderBy(mb => mb.Name)
            .ToListAsync();
        return Result<List<MovementBase>>.Success(movementBases);
    }

    [McpServerTool, Description("Create a new movement base for a user.")]
    public async Task<Result<MovementBase>> CreateMovementBaseAsync(IdentityUser user, CreateMovementBaseRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        // Check if movement base with this name already exists for this user
        var existingBase = await _context.MovementBases
            .FirstOrDefaultAsync(mb => mb.Name.ToLower() == request.Name.ToLower() && mb.UserID == userGuid);

        if (existingBase != null)
        {
            return Result<MovementBase>.Conflict("A movement base with this name already exists for this user.");
        }

        var movementBase = new MovementBase
        {
            MovementBaseID = Guid.NewGuid(),
            Name = request.Name,
            UserID = userGuid
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
        var session = await _context.TrainingSessions
            .Include(ts => ts.TrainingProgram)
            .Include(ts => ts.Movements)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == request.TrainingSessionID &&
                                   ts.TrainingProgram!.UserID == userGuid);
        if (session is null)
            return Result.NotFound("Training session not found or access denied.");

        var sessionMovements = session.Movements.ToDictionary(m => m.MovementID);
        var requestIds = request.Movements.Select(m => m.MovementID).ToHashSet();

        // Validate all session movements are present in the request and vice versa
        if (!(new HashSet<Guid>(sessionMovements.Keys)).SetEquals(requestIds))
            return Result.Invalid(new List<ValidationError> {
            new ValidationError { ErrorMessage = "Movement IDs don't match exactly with session movements." }
        });

        // Update ordering
        foreach (var update in request.Movements)
        {
            sessionMovements[update.MovementID].Ordering = update.Ordering;
        }

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    [McpServerTool, Description("Delete a movement base for a user.")]
    public async Task<Result> DeleteMovementBaseAsync(IdentityUser user, Guid movementBaseId)
    {
        var userGuid = Guid.Parse(user.Id);
        // Verify the base exists and belongs to the user
        var movementBase = await _context.MovementBases.FirstOrDefaultAsync(mb => mb.MovementBaseID == movementBaseId && mb.UserID == userGuid);
        if (movementBase == null)
        {
            return Result.NotFound("Movement base not found or not owned by user.");
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

    public async Task<Result<Equipment>> CreateEquipmentAsync(IdentityUser user, CreateEquipmentRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        // Check if equipment with this name already exists for this user
        var existingEquipment = await _context.Equipments
            .FirstOrDefaultAsync(e => e.Name.ToLower() == request.Name.ToLower() && e.UserID == userGuid);

        if (existingEquipment != null)
        {
            return Result<Equipment>.Conflict("An equipment with this name already exists for this user.");
        }

        var equipment = new Equipment
        {
            EquipmentID = Guid.NewGuid(),
            Name = request.Name,
            UserID = userGuid
        };

        _context.Equipments.Add(equipment);
        await _context.SaveChangesAsync();
        return Result<Equipment>.Created(equipment);
    }

    public async Task<Result> DeleteEquipmentAsync(IdentityUser user, Guid equipmentId)
    {
        var userGuid = Guid.Parse(user.Id);
        // Verify the equipment exists and belongs to the user
        var equipment = await _context.Equipments.FirstOrDefaultAsync(e => e.EquipmentID == equipmentId && e.UserID == userGuid);
        if (equipment == null)
        {
            return Result.NotFound("Equipment not found or not owned by user.");
        }

         // Prevent deleting a equipment that’s in use
        var inUse = await _context.Movements.AnyAsync(m => m.MovementModifier.EquipmentID == equipmentId);
        if (inUse)
        {
            return Result.Conflict("Cannot delete equipment while it has associated movements.");
        }

        // Remove and save
        _context.Equipments.Remove(equipment);
        await _context.SaveChangesAsync();
        return Result.NoContent();
    }

    public async Task<Result<List<Equipment>>> GetEquipmentsAsync(IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);
        var equipments = await _context.Equipments
            .Where(e => e.UserID == userGuid)
            .OrderBy(e => e.Name)
            .ToListAsync();
        return Result<List<Equipment>>.Success(equipments);
    }



}