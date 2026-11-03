using lionheart.Functional;
using lionheart.Data;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Training
{
    public interface IMovementBaseService
    {
        Task<Result<List<MovementBaseDTO>, Problem>> GetMovementBasesAsync(IdentityUser user);
        Task<Result<MovementBaseDTO, Problem>> CreateMovementBaseAsync(IdentityUser user, CreateMovementBaseRequest request);
        Task<Result<Unit, Problem>> DeleteMovementBaseAsync(IdentityUser user, Guid movementBaseId);
        Task<Result<MovementBaseDTO, Problem>> UpdateMovementBaseAsync(IdentityUser user, UpdateMovementBaseRequest request);
        Task<Result<List<MuscleGroup>, Problem>> GetAllMuscleGroupsAsync();
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

        public async Task<Result<MovementBaseDTO, Problem>> CreateMovementBaseAsync(IdentityUser user, CreateMovementBaseRequest request)
        {
            var userGuid = Guid.Parse(user.Id);

            var existingBase = await _context.MovementBases
                .FirstOrDefaultAsync(mb => mb.Name.ToLower() == request.Name.ToLower() && mb.UserID == userGuid);

            if (existingBase != null)
            {
                return Result<MovementBaseDTO, Problem>.Failure(new Problem.Conflict("A movement base with this name already exists for this user."));
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
                return Result<MovementBaseDTO, Problem>.Failure(new Problem.Invalid(
                    nameof(request.MuscleGroups), "One or more muscle groups do not exist."));
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
            return Result<MovementBaseDTO, Problem>.Success(movementBase.ToDTO());
        }

        public async Task<Result<Unit, Problem>> DeleteMovementBaseAsync(IdentityUser user, Guid movementBaseId)
        {
            var userGuid = Guid.Parse(user.Id);

            var movementBase = await _context.MovementBases
                .FirstOrDefaultAsync(mb => mb.MovementBaseID == movementBaseId && mb.UserID == userGuid);

            if (movementBase == null)
            {
                return Result<Unit, Problem>.Failure(new Problem.NotFound("Movement base not found or not owned by user."));
            }

            var inUse = await _context.MovementDatas.AnyAsync(md => md.MovementBaseID == movementBaseId);
            if (inUse)
            {
                return Result<Unit, Problem>.Failure(new Problem.Conflict("Cannot delete movement base while movements reference it."));
            }

            _context.MovementBases.Remove(movementBase);
            await _context.SaveChangesAsync();
            return Result<Unit, Problem>.Success(Unit.Value);
        }

        public async Task<Result<List<MovementBaseDTO>, Problem>> GetMovementBasesAsync(IdentityUser user)
        {
            var userGuid = Guid.Parse(user.Id);

            var movementBases = await _context.MovementBases
                .Where(mb => mb.UserID == userGuid)
                .Include(mb => mb.MuscleGroups)
                .OrderBy(mb => mb.Name)
                .ToListAsync();

            var dtos = movementBases.Select(mb => mb.ToDTO()).ToList();
            return Result<List<MovementBaseDTO>, Problem>.Success(dtos);
        }

        public async Task<Result<List<MuscleGroup>, Problem>> GetAllMuscleGroupsAsync()
        {
            var muscleGroups = await _context.MuscleGroups.ToListAsync();
            return Result<List<MuscleGroup>, Problem>.Success(muscleGroups);
        }

        public async Task<Result<MovementBaseDTO, Problem>> UpdateMovementBaseAsync(IdentityUser user, UpdateMovementBaseRequest request)
        {
            var userGuid = Guid.Parse(user.Id);

            var movementBase = await _context.MovementBases
                .Include(mb => mb.MuscleGroups)
                .FirstOrDefaultAsync(mb => mb.MovementBaseID == request.MovementBaseID && mb.UserID == userGuid);

            if (movementBase == null)
            {
                return Result<MovementBaseDTO, Problem>.Failure(new Problem.NotFound("Movement base not found or not owned by user."));
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
                return Result<MovementBaseDTO, Problem>.Failure(new Problem.Invalid(
                    nameof(request.MuscleGroups), "One or more muscle groups do not exist."));
            }

            movementBase.MuscleGroups = muscleGroups;

            await _context.SaveChangesAsync();
            return Result<MovementBaseDTO, Problem>.Success(movementBase.ToDTO());
        }
    }
}