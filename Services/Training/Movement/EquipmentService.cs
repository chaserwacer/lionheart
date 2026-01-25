using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Training;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public interface IEquipmentService
{
    Task<Result<EquipmentDTO>> CreateEquipmentAsync(IdentityUser user, CreateEquipmentRequest request);
    Task<Result<EquipmentDTO>> UpdateEquipmentAsync(IdentityUser user, UpdateEquipmentRequest request);
    Task<Result> DeleteEquipmentAsync(IdentityUser user, Guid equipmentId);
    Task<Result<List<EquipmentDTO>>> GetEquipmentsAsync(IdentityUser user);
}

public class EquipmentService : IEquipmentService
{
    private readonly ModelContext _context;

    public EquipmentService(ModelContext context)
    {
        _context = context;
    }

    public async Task<Result<EquipmentDTO>> CreateEquipmentAsync(IdentityUser user, CreateEquipmentRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        // Check if equipment with this name already exists for this user
        var existingEquipment = await _context.Equipments
            .FirstOrDefaultAsync(e => e.Name.ToLower() == request.Name.ToLower() && e.UserID == userGuid);

        if (existingEquipment != null)
        {
            return Result<EquipmentDTO>.Conflict("An equipment with this name already exists for this user.");
        }

        var equipment = new Equipment
        {
            EquipmentID = Guid.NewGuid(),
            Name = request.Name,
            UserID = userGuid,
            Enabled = true
        };

        _context.Equipments.Add(equipment);
        await _context.SaveChangesAsync();
        return Result<EquipmentDTO>.Created(equipment.AdaptToDTO());
    }

    public async Task<Result<EquipmentDTO>> UpdateEquipmentAsync(IdentityUser user, UpdateEquipmentRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var equipment = await _context.Equipments
            .FirstOrDefaultAsync(e => e.EquipmentID == request.EquipmentID && e.UserID == userGuid);

        if (equipment == null)
        {
            return Result.NotFound("Equipment not found or not owned by user.");
        }

        // Check for name conflicts
        var normalizedName = request.Name.Trim().ToLower();
        var nameConflict = await _context.Equipments
            .AnyAsync(e => e.UserID == userGuid && e.EquipmentID != request.EquipmentID && e.Name.ToLower() == normalizedName);

        if (nameConflict)
        {
            return Result.Conflict("An equipment with this name already exists for this user.");
        }

        equipment.Name = request.Name;
        equipment.Enabled = request.Enabled;

        await _context.SaveChangesAsync();
        return Result<EquipmentDTO>.Success(equipment.AdaptToDTO());
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

        // Prevent deleting equipment that's in use
        var inUse = await _context.Movements.AnyAsync(m => m.MovementData.EquipmentID == equipmentId);
        if (inUse)
        {
            return Result.Conflict("Cannot delete equipment while it has associated movements.");
        }

        // Remove and save
        _context.Equipments.Remove(equipment);
        await _context.SaveChangesAsync();
        return Result.NoContent();
    }

    public async Task<Result<List<EquipmentDTO>>> GetEquipmentsAsync(IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);
        var equipments = await _context.Equipments
            .Where(e => e.UserID == userGuid)
            .OrderBy(e => e.Name)
            .ToListAsync();
        return Result<List<EquipmentDTO>>.Success(equipments.Select(e => e.AdaptToDTO()).ToList());
    }
}
