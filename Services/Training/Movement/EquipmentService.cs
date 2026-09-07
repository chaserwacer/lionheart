using lionheart.Functional;
using lionheart.Data;
using lionheart.Model.Training;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public interface IEquipmentService
{
    Task<Result<EquipmentDTO, Problem>> CreateEquipmentAsync(IdentityUser user, CreateEquipmentRequest request);
    Task<Result<EquipmentDTO, Problem>> UpdateEquipmentAsync(IdentityUser user, UpdateEquipmentRequest request);
    Task<Result<Unit, Problem>> DeleteEquipmentAsync(IdentityUser user, Guid equipmentId);
    Task<Result<List<EquipmentDTO>, Problem>> GetEquipmentsAsync(IdentityUser user);
}

public class EquipmentService : IEquipmentService
{
    private readonly ModelContext _context;

    public EquipmentService(ModelContext context)
    {
        _context = context;
    }

    public async Task<Result<EquipmentDTO, Problem>> CreateEquipmentAsync(IdentityUser user, CreateEquipmentRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        // Check if equipment with this name already exists for this user
        var existingEquipment = await _context.Equipments
            .FirstOrDefaultAsync(e => e.Name.ToLower() == request.Name.ToLower() && e.UserID == userGuid);

        if (existingEquipment != null)
        {
            return Result<EquipmentDTO, Problem>.Failure(new Problem.Conflict("An equipment with this name already exists for this user."));
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
        return Result<EquipmentDTO, Problem>.Success(equipment.ToDTO());
    }

    public async Task<Result<EquipmentDTO, Problem>> UpdateEquipmentAsync(IdentityUser user, UpdateEquipmentRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var equipment = await _context.Equipments
            .FirstOrDefaultAsync(e => e.EquipmentID == request.EquipmentID && e.UserID == userGuid);

        if (equipment == null)
        {
            return Result<EquipmentDTO, Problem>.Failure(new Problem.NotFound("Equipment not found or not owned by user."));
        }

        // Check for name conflicts
        var normalizedName = request.Name.Trim().ToLower();
        var nameConflict = await _context.Equipments
            .AnyAsync(e => e.UserID == userGuid && e.EquipmentID != request.EquipmentID && e.Name.ToLower() == normalizedName);

        if (nameConflict)
        {
            return Result<EquipmentDTO, Problem>.Failure(new Problem.Conflict("An equipment with this name already exists for this user."));
        }

        equipment.Name = request.Name;
        equipment.Enabled = request.Enabled;

        await _context.SaveChangesAsync();
        return Result<EquipmentDTO, Problem>.Success(equipment.ToDTO());
    }

    public async Task<Result<Unit, Problem>> DeleteEquipmentAsync(IdentityUser user, Guid equipmentId)
    {
        var userGuid = Guid.Parse(user.Id);
        // Verify the equipment exists and belongs to the user
        var equipment = await _context.Equipments.FirstOrDefaultAsync(e => e.EquipmentID == equipmentId && e.UserID == userGuid);
        if (equipment == null)
        {
            return Result<Unit, Problem>.Failure(new Problem.NotFound("Equipment not found or not owned by user."));
        }

        // Prevent deleting equipment that's in use
        var inUse = await _context.MovementDatas.AnyAsync(md => md.EquipmentID == equipmentId);
        if (inUse)
        {
            return Result<Unit, Problem>.Failure(new Problem.Conflict("Cannot delete equipment while movements reference it."));
        }

        // Remove and save
        _context.Equipments.Remove(equipment);
        await _context.SaveChangesAsync();
        return Result<Unit, Problem>.Success(Unit.Value);
    }

    public async Task<Result<List<EquipmentDTO>, Problem>> GetEquipmentsAsync(IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);
        var equipments = await _context.Equipments
            .Where(e => e.UserID == userGuid)
            .OrderBy(e => e.Name)
            .ToListAsync();
        return Result<List<EquipmentDTO>, Problem>.Success(equipments.Select(e => e.ToDTO()).ToList());
    }
}
