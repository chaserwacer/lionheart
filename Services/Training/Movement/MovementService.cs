using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Training;
using lionheart.Model.Training.SetEntry;
using lionheart.Services;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public interface IMovementService
{
    Task<Result<List<MovementDTO>>> GetMovementsAsync(IdentityUser user, Guid sessionId);
    Task<Result<MovementDTO>> CreateMovementAsync(IdentityUser user, CreateMovementRequest request);
    Task<Result<MovementDTO>> UpdateMovementAsync(IdentityUser user, UpdateMovementRequest request);
    Task<Result> DeleteMovementAsync(IdentityUser user, Guid movementId);
    Task<Result> UpdateMovementOrder(IdentityUser user, UpdateMovementOrderRequest request);

}
public class MovementService : IMovementService
{
    private readonly ModelContext _context;
    private readonly IMovementDataService _movementDataService;

    public MovementService(ModelContext context, IMovementDataService movementDataService)
    {
        _context = context;
        _movementDataService = movementDataService;
    }

    /// <summary>
    /// Finds an existing MovementData that matches the given parameters, or creates a new one.
    /// This ensures MovementData is reused for identical movement configurations.
    /// </summary>
    private async Task<MovementDataDTO> FindOrCreateMovementDataAsync(
        IdentityUser user,
        Guid equipmentId,
        Guid movementBaseId,
        Guid? movementModifierId,
        Equipment equipment,
        MovementBase movementBase,
        MovementModifier? movementModifier)
    {
        return await _movementDataService.FindOrCreateMovementDataAsync(
            user,
            new CreateMovementDataRequest(
                EquipmentID: equipmentId,
                MovementBaseID: movementBaseId,
                MovementModifierID: movementModifierId
            )
        );
    }

    public async Task<Result<List<MovementDTO>>> GetMovementsAsync(IdentityUser user, Guid sessionId)
    {
        var userGuid = Guid.Parse(user.Id);

        var session = await _context.TrainingSessions
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == sessionId && ts.UserID == userGuid);

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

        var session = await _context.TrainingSessions
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == request.TrainingSessionID && ts.UserID == userGuid);

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
            movementModifier = await _context.MovementModifiers
                .FirstOrDefaultAsync(mm => mm.MovementModifierID == request.MovementData.MovementModifierID.Value && mm.UserID == userGuid);
            if (movementModifier == null)
            {
                return Result<MovementDTO>.NotFound("Movement modifier not found.");
            }
        }

        // Find or create MovementData
        var movementData = await FindOrCreateMovementDataAsync(
            user,
            equipment.EquipmentID,
            movementBase.MovementBaseID,
            movementModifier?.MovementModifierID,
            equipment,
            movementBase,
            movementModifier);

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
            MovementDataID = movementData.MovementDataID,
            IsCompleted = false,
            Ordering = maxOrdering + 1,
            LiftSets = new List<LiftSetEntry>(),
            DistanceTimeSets = new List<DTSetEntry>()
        };

        _context.Movements.Add(movement);
        await _context.SaveChangesAsync();

        return Result<MovementDTO>.Created(movement.ToDTO());
    }

    public async Task<Result<MovementDTO>> UpdateMovementAsync(IdentityUser user, UpdateMovementRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var movement = await _context.Movements
            .Include(m => m.TrainingSession)
            .Include(m => m.MovementData)
            .FirstOrDefaultAsync(m => m.MovementID == request.MovementID);

        if (movement is null)
        {
            return Result<MovementDTO>.NotFound("Movement not found.");
        }

        if (movement.TrainingSession.UserID != userGuid)
        {
            return Result<MovementDTO>.Unauthorized();
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
            movementModifier = await _context.MovementModifiers
                .FirstOrDefaultAsync(mm => mm.MovementModifierID == request.MovementData.MovementModifierID.Value && mm.UserID == userGuid);
            if (movementModifier == null)
            {
                return Result<MovementDTO>.NotFound("Movement modifier not found.");
            }
        }

        // Find or create MovementData
        var movementData = await FindOrCreateMovementDataAsync(
            user,
            equipment.EquipmentID,
            movementBase.MovementBaseID,
            movementModifier?.MovementModifierID,
            equipment,
            movementBase,
            movementModifier);

        movement.Notes = request.Notes;
        movement.MovementDataID = movementData.MovementDataID;
        // TODO: CHECK if we need to update navigation property too
        // movement.MovementData = movementData;
        movement.IsCompleted = request.IsCompleted;

        await _context.SaveChangesAsync();
        return Result<MovementDTO>.Success(movement.Adapt<MovementDTO>());
    }

    public async Task<Result> DeleteMovementAsync(IdentityUser user, Guid movementId)
    {
        var userGuid = Guid.Parse(user.Id);
        var movement = await _context.Movements
            .Include(m => m.TrainingSession)
            .FirstOrDefaultAsync(m => m.MovementID == movementId);

        if (movement is null)
        {
            return Result.NotFound("Movement not found.");
        }

        if (movement.TrainingSession.UserID != userGuid)
        {
            return Result.Unauthorized();
        }

        _context.Movements.Remove(movement);
        await _context.SaveChangesAsync();
        return Result.NoContent();
    }

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
}