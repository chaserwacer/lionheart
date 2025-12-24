using System.ComponentModel;
using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.DTOs;
using lionheart.Model.Training;
using lionheart.Model.Training.SetEntry;
using lionheart.Services;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

public interface IMovementService
{
    Task<Result<List<MovementDTO>>> GetMovementsAsync(IdentityUser user, Guid sessionId);
    Task<Result<MovementDTO>> CreateMovementAsync(IdentityUser user, CreateMovementRequest request);
    Task<Result<MovementDTO>> UpdateMovementAsync(IdentityUser user, UpdateMovementRequest request);
    Task<Result> DeleteMovementAsync(IdentityUser user, Guid movementId);
}
public class MovementService : IMovementService
{
    private readonly ModelContext _context;

    public MovementService(ModelContext context)
    {
        _context = context;
    }

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
            .Include(m => m.LiftSets)
            .Include(m => m.DistanceTimeSets)
            .Include(m => m.MovementData).ThenInclude(md => md.Equipment)
            .Include(m => m.MovementData).ThenInclude(md => md.MovementBase)
            .Include(m => m.MovementData).ThenInclude(md => md.MovementModifier)
            .OrderBy(m => m.Ordering)
            .ToListAsync();

        return Result<List<MovementDTO>>.Success([.. movements.Select(m => m.Adapt<MovementDTO>())]);
    }


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
        var movementBase = await _context.MovementBases.FindAsync(request.MovementData.MovementBaseID);

        if (movementBase is null)
        {
            return Result<MovementDTO>.NotFound("Movement base not found.");
        }

        var equipment = await _context.Equipments.FindAsync(request.MovementData.EquipmentID);
        if (equipment == null)
        {
            return Result<MovementDTO>.NotFound("Equipment not found.");
        }

        // Verify movement modifier if provided
        MovementModifier? movementModifier = null;
        if (request.MovementData.MovementModifierID.HasValue)
        {
            movementModifier = await _context.Set<MovementModifier>()
                .FirstOrDefaultAsync(mm => mm.MovementModifierID == request.MovementData.MovementModifierID.Value && mm.UserID == userGuid);
            if (movementModifier == null)
            {
                return Result<MovementDTO>.NotFound("Movement modifier not found.");
            }
        }

        var orderings = await _context.Movements
            .Where(m => m.TrainingSessionID == request.TrainingSessionID)
            .Select(m => (int?)m.Ordering)
            .ToListAsync();
        var maxOrdering = orderings.Count > 0 && orderings.Any(o => o.HasValue) ? orderings.Max() ?? -1 : -1;

        var movement = new Movement
        {
            MovementID = Guid.NewGuid(),
            TrainingSessionID = request.TrainingSessionID,
            Notes = request.Notes,
            MovementData = new MovementData
            {
                MovementDataID = Guid.NewGuid(),
                UserID = userGuid,
                EquipmentID = equipment.EquipmentID,
                Equipment = equipment,
                MovementBaseID = movementBase.MovementBaseID,
                MovementBase = movementBase,
                MovementModifierID = movementModifier?.MovementModifierID,
                MovementModifier = movementModifier
            },
            IsCompleted = false,
            Ordering = maxOrdering + 1,
            LiftSets = new List<LiftSetEntry>(),
            DistanceTimeSets = new List<DTSetEntry>()
        };

        _context.Movements.Add(movement);
        await _context.SaveChangesAsync();

        return Result<MovementDTO>.Created(movement.Adapt<MovementDTO>());
    }

    public async Task<Result<MovementDTO>> UpdateMovementAsync(IdentityUser user, UpdateMovementRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var movement = await _context.Movements
            .Include(m => m.TrainingSession)
            .ThenInclude(ts => ts.TrainingProgram)
            .Include(m => m.MovementData)
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
        var movementBase = await _context.MovementBases.FindAsync(request.MovementData.MovementBaseID);

        if (movementBase == null)
        {
            return Result<MovementDTO>.NotFound("Movement base not found.");
        }

        var equipment = await _context.Equipments.FindAsync(request.MovementData.EquipmentID);
        if (equipment == null)
        {
            return Result<MovementDTO>.NotFound("Equipment not found.");
        }

        // Verify movement modifier if provided
        MovementModifier? movementModifier = null;
        if (request.MovementData.MovementModifierID.HasValue)
        {
            movementModifier = await _context.Set<MovementModifier>()
                .FirstOrDefaultAsync(mm => mm.MovementModifierID == request.MovementData.MovementModifierID.Value && mm.UserID == userGuid);
            if (movementModifier == null)
            {
                return Result<MovementDTO>.NotFound("Movement modifier not found.");
            }
        }

        movement.Notes = request.Notes;
        movement.MovementData = new MovementData
        {
            MovementDataID = Guid.NewGuid(),
            UserID = userGuid,
            EquipmentID = equipment.EquipmentID,
            Equipment = equipment,
            MovementBaseID = movementBase.MovementBaseID,
            MovementBase = movementBase,
            MovementModifierID = movementModifier?.MovementModifierID,
            MovementModifier = movementModifier
        };
        movement.IsCompleted = request.IsCompleted;

        await _context.SaveChangesAsync();
        return Result<MovementDTO>.Success(movement.Adapt<MovementDTO>());
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
}