using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Training;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services;

public interface IMovementDataService
{
    Task<Result<MovementDataDTO>> GetMovementDataAsync(IdentityUser user, Guid movementDataId);
    Task<Result<List<MovementDataDTO>>> GetMovementDatasAsync(IdentityUser user);
    Task<Result<MovementDataDTO>> CreateMovementDataAsync(IdentityUser user, CreateMovementDataRequest request);
    Task<Result<MovementDataDTO>> FindOrCreateMovementDataAsync(IdentityUser user, CreateMovementDataRequest request);
}

public class MovementDataService : IMovementDataService
{
    private readonly ModelContext _context;

    public MovementDataService(ModelContext context)
    {
        _context = context;
    }

    public async Task<Result<MovementDataDTO>> GetMovementDataAsync(IdentityUser user, Guid movementDataId)
    {
        var userGuid = Guid.Parse(user.Id);
        var movementData = await _context.MovementDatas
            .Include(md => md.Equipment)
            .Include(md => md.MovementBase)
            .Include(md => md.MovementModifier)
            .FirstOrDefaultAsync(md => md.MovementDataID == movementDataId && md.UserID == userGuid);

        if (movementData == null)
        {
            return Result<MovementDataDTO>.NotFound("MovementData not found or access denied.");
        }

        return Result<MovementDataDTO>.Success(movementData.ToDTO());
    }

    public async Task<Result<List<MovementDataDTO>>> GetMovementDatasAsync(IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);
        var movementDatas = await _context.MovementDatas
            .Where(md => md.UserID == userGuid)
            .Include(md => md.Equipment)
            .Include(md => md.MovementBase)
            .Include(md => md.MovementModifier)
            .OrderBy(md => md.MovementBase.Name)
            .ThenBy(md => md.Equipment.Name)
            .ToListAsync();

        return Result<List<MovementDataDTO>>.Success(movementDatas.Select(md => md.ToDTO()).ToList());
    }

    public async Task<Result<MovementDataDTO>> CreateMovementDataAsync(IdentityUser user, CreateMovementDataRequest request)
    {
        var userGuid = Guid.Parse(user.Id);

        // Check if this combination already exists
        var existing = await _context.MovementDatas
            .Include(md => md.Equipment)
            .Include(md => md.MovementBase)
            .Include(md => md.MovementModifier)
            .FirstOrDefaultAsync(md =>
                md.UserID == userGuid &&
                md.EquipmentID == request.EquipmentID &&
                md.MovementBaseID == request.MovementBaseID &&
                md.MovementModifierID == request.MovementModifierID);

        if (existing != null)
        {
            return Result<MovementDataDTO>.Conflict("A MovementData with this combination already exists.");
        }

        // Verify equipment exists and belongs to user
        var equipment = await _context.Equipments
            .FirstOrDefaultAsync(e => e.EquipmentID == request.EquipmentID && e.UserID == userGuid);
        if (equipment == null)
        {
            return Result<MovementDataDTO>.NotFound("Equipment not found or access denied.");
        }

        // Verify movement base exists and belongs to user
        var movementBase = await _context.MovementBases
            .FirstOrDefaultAsync(mb => mb.MovementBaseID == request.MovementBaseID && mb.UserID == userGuid);
        if (movementBase == null)
        {
            return Result<MovementDataDTO>.NotFound("MovementBase not found or access denied.");
        }

        // Verify movement modifier if provided
        MovementModifier? movementModifier = null;
        if (request.MovementModifierID.HasValue)
        {
            movementModifier = await _context.MovementModifiers
                .FirstOrDefaultAsync(mm => mm.MovementModifierID == request.MovementModifierID.Value && mm.UserID == userGuid);
            if (movementModifier == null)
            {
                return Result<MovementDataDTO>.NotFound("MovementModifier not found or access denied.");
            }
        }

        var movementData = new MovementData
        {
            MovementDataID = Guid.NewGuid(),
            UserID = userGuid,
            EquipmentID = equipment.EquipmentID,
            Equipment = equipment,
            MovementBaseID = movementBase.MovementBaseID,
            MovementBase = movementBase,
            MovementModifierID = movementModifier?.MovementModifierID,
            MovementModifier = movementModifier,
            CreatedAt = DateTime.UtcNow
        };

        _context.MovementDatas.Add(movementData);
        await _context.SaveChangesAsync();

        return Result<MovementDataDTO>.Created(movementData.ToDTO());
    }

    public async Task<Result<MovementDataDTO>> FindOrCreateMovementDataAsync(IdentityUser user, CreateMovementDataRequest request)
    {
        var userGuid = Guid.Parse(user.Id);

        // Try to find existing
        var existing = await _context.MovementDatas
            .Include(md => md.Equipment)
            .Include(md => md.MovementBase)
            .Include(md => md.MovementModifier)
            .FirstOrDefaultAsync(md =>
                md.UserID == userGuid &&
                md.EquipmentID == request.EquipmentID &&
                md.MovementBaseID == request.MovementBaseID &&
                md.MovementModifierID == request.MovementModifierID);

        if (existing != null)
        {
            return Result<MovementDataDTO>.Success(existing.ToDTO());
        }

        // Create new if not found
        return await CreateMovementDataAsync(user, request);
    }
}
