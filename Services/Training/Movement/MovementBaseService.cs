using lionheart.Functional;
using lionheart.Data;
using lionheart.Services.Training.Patterns;
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
    }

    public class MovementBaseService : IMovementBaseService
    {
        private readonly ModelContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly MovementPatternMap _map;

        public MovementBaseService(ModelContext context, UserManager<IdentityUser> userManager, MovementPatternMap map)
        {
            _context = context;
            _userManager = userManager;
            _map = map;
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

            var movementBase = new MovementBase
            {
                MovementBaseID = Guid.NewGuid(),
                Description = request.Description,
                Name = request.Name,
                UserID = userGuid,
            };

            _context.MovementBases.Add(movementBase);
            await _context.SaveChangesAsync();
            return Result<MovementBaseDTO, Problem>.Success(movementBase.ToDTO(_map.StandingOf(movementBase)));
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
                .OrderBy(mb => mb.Name)
                .ToListAsync();

            var dtos = movementBases.Select(mb => mb.ToDTO(_map.StandingOf(mb))).ToList();
            return Result<List<MovementBaseDTO>, Problem>.Success(dtos);
        }

        public async Task<Result<MovementBaseDTO, Problem>> UpdateMovementBaseAsync(IdentityUser user, UpdateMovementBaseRequest request)
        {
            var userGuid = Guid.Parse(user.Id);

            var movementBase = await _context.MovementBases
                .FirstOrDefaultAsync(mb => mb.MovementBaseID == request.MovementBaseID && mb.UserID == userGuid);

            if (movementBase == null)
            {
                return Result<MovementBaseDTO, Problem>.Failure(new Problem.NotFound("Movement base not found or not owned by user."));
            }

            movementBase.Name = request.Name;
            movementBase.Description = request.Description;

            await _context.SaveChangesAsync();
            return Result<MovementBaseDTO, Problem>.Success(movementBase.ToDTO(_map.StandingOf(movementBase)));
        }
    }
}