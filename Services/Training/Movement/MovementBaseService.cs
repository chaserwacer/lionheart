using Ardalis.Result;
using lionheart.Data;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace lionheart.Services.Training
{
    public interface IMovementBaseService
    {
        Task<Result<List<MovementBaseDTO>>> GetMovementBasesAsync(IdentityUser user);
        Task<Result<MovementBaseDTO>> CreateMovementBaseAsync(IdentityUser user, CreateMovementBaseRequest request);
        Task<Result> DeleteMovementBaseAsync(IdentityUser user, Guid movementBaseId);
        Task<Result<MovementBaseDTO>> UpdateMovementBaseAsync(IdentityUser user, UpdateMovementBaseRequest request);
        Task<Result<List<MuscleGroup>>> GetAllMuscleGroupsAsync();
    }
    public class MovementBaseService : IMovementBaseService
    {
        private readonly ModelContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public MovementBaseService(ModelContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<Result<MovementBaseDTO>> CreateMovementBaseAsync(IdentityUser user, CreateMovementBaseRequest request)
        {
            var userGuid = Guid.Parse(user.Id);

            // Check if movement base with this name already exists for this user
            var existingBase = await _context.MovementBases
                .FirstOrDefaultAsync(mb => mb.Name.ToLower() == request.Name.ToLower() && mb.UserID == userGuid);

            if (existingBase != null)
            {
                return Result<MovementBaseDTO>.Conflict("A movement base with this name already exists for this user.");
            }

            var movementBase = new MovementBase
            {
                MovementBaseID = Guid.NewGuid(),
                MuscleGroups = request.MuscleGroups,
                Description = request.Description,
                Name = request.Name,
                UserID = userGuid
            };

            _context.MovementBases.Add(movementBase);
            await _context.SaveChangesAsync();
            return Result<MovementBaseDTO>.Created(movementBase.Adapt<MovementBaseDTO>());
        }

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
            var inUse = await _context.Movements.AnyAsync(m => m.MovementData.MovementBaseID == movementBaseId);
            if (inUse)
            {
                return Result.Conflict("Cannot delete movement base while it has associated movements.");
            }

            // Remove and save
            _context.MovementBases.Remove(movementBase);
            await _context.SaveChangesAsync();
            return Result.NoContent();
        }

        public async Task<Result<List<MovementBaseDTO>>> GetMovementBasesAsync(IdentityUser user)
        {
            var userGuid = Guid.Parse(user.Id);
            var movementBases = await _context.MovementBases
                .Where(mb => mb.UserID == userGuid)
                .OrderBy(mb => mb.Name)
                .ToListAsync();
            return Result<List<MovementBaseDTO>>.Success(movementBases.Adapt<List<MovementBaseDTO>>());
        }

        public async Task<Result<List<MuscleGroup>>> GetAllMuscleGroupsAsync()
        {
            var muscleGroups = await _context.MuscleGroups.ToListAsync();
            return Result<List<MuscleGroup>>.Success(muscleGroups);
        }

        public async Task<Result<MovementBaseDTO>> UpdateMovementBaseAsync(IdentityUser user, UpdateMovementBaseRequest request)
        {
            var userGuid = Guid.Parse(user.Id);
            var movementBase = await _context.MovementBases
                .Include(mb => mb.MuscleGroups)
                .FirstOrDefaultAsync(mb => mb.MovementBaseID == request.MovementBaseID && mb.UserID == userGuid);
            if (movementBase == null)
            {
                return Result<MovementBaseDTO>.NotFound("Movement base not found or not owned by user.");
            }
            movementBase.Name = request.Name;
            movementBase.Description = request.Description;

            // Clear and recreate owned entities to properly handle EF Core tracking
            movementBase.MuscleGroups = request.MuscleGroups;

            {

                await _context.SaveChangesAsync();
                return Result<MovementBaseDTO>.Success(movementBase.Adapt<MovementBaseDTO>());
            }
        }
    }
}