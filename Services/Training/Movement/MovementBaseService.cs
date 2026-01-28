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

            var existingBase = await _context.MovementBases
                .FirstOrDefaultAsync(mb => mb.Name.ToLower() == request.Name.ToLower() && mb.UserID == userGuid);

            if (existingBase != null)
            {
                return Result<MovementBaseDTO>.Conflict("A movement base with this name already exists for this user.");
            }

            // IMPORTANT: resolve requested muscle groups to existing tracked entities
            var requestedIds = (request.MuscleGroups ?? new List<MuscleGroup>())
                .Select(mg => mg.MuscleGroupID)
                .Distinct()
                .ToList();

            var muscleGroups = requestedIds.Count == 0
                ? new List<MuscleGroup>()
                : await _context.MuscleGroups
                    .Where(mg => requestedIds.Contains(mg.MuscleGroupID))
                    .ToListAsync();

            if (muscleGroups.Count != requestedIds.Count)
            {
                return Result<MovementBaseDTO>.Invalid(new ValidationError
                {
                    Identifier = nameof(request.MuscleGroups),
                    ErrorMessage = "One or more muscle groups do not exist."
                });
            }

            var movementBase = new MovementBase
            {
                MovementBaseID = Guid.NewGuid(),
                Description = request.Description,
                Name = request.Name,
                UserID = userGuid,
                MuscleGroups = muscleGroups
            };

            _context.MovementBases.Add(movementBase);
            await _context.SaveChangesAsync();
            return Result<MovementBaseDTO>.Created(movementBase.Adapt<MovementBaseDTO>());
        }

        public async Task<Result> DeleteMovementBaseAsync(IdentityUser user, Guid movementBaseId)
        {
            var userGuid = Guid.Parse(user.Id);

            var movementBase = await _context.MovementBases
                .FirstOrDefaultAsync(mb => mb.MovementBaseID == movementBaseId && mb.UserID == userGuid);

            if (movementBase == null)
            {
                return Result.NotFound("Movement base not found or not owned by user.");
            }

            var inUse = await _context.Movements.AnyAsync(m => m.MovementData.MovementBaseID == movementBaseId);
            if (inUse)
            {
                return Result.Conflict("Cannot delete movement base while it has associated movements.");
            }

            _context.MovementBases.Remove(movementBase);
            await _context.SaveChangesAsync();
            return Result.NoContent();
        }

        public async Task<Result<List<MovementBaseDTO>>> GetMovementBasesAsync(IdentityUser user)
        {
            var userGuid = Guid.Parse(user.Id);

            var movementBases = await _context.MovementBases
                .Where(mb => mb.UserID == userGuid)
                .Include(mb => mb.MuscleGroups)
                .OrderBy(mb => mb.Name)
                .ToListAsync();

            var dtos = movementBases.Select(mb => mb.ToDTO()).ToList();
            return Result<List<MovementBaseDTO>>.Success(dtos);
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

            // IMPORTANT: resolve requested muscle groups to existing tracked entities
            var requestedIds = (request.MuscleGroups ?? new List<MuscleGroup>())
                .Select(mg => mg.MuscleGroupID)
                .Distinct()
                .ToList();

            var muscleGroups = requestedIds.Count == 0
                ? new List<MuscleGroup>()
                : await _context.MuscleGroups
                    .Where(mg => requestedIds.Contains(mg.MuscleGroupID))
                    .ToListAsync();

            if (muscleGroups.Count != requestedIds.Count)
            {
                return Result<MovementBaseDTO>.Invalid(new ValidationError
                {
                    Identifier = nameof(request.MuscleGroups),
                    ErrorMessage = "One or more muscle groups do not exist."
                });
            }

            movementBase.MuscleGroups = muscleGroups;

            await _context.SaveChangesAsync();
            return Result<MovementBaseDTO>.Success(movementBase.Adapt<MovementBaseDTO>());
        }
    }
}