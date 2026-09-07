using lionheart.Functional;
using lionheart.Model.InjuryManagement;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using lionheart.Data;
using Model.Chat.Tools;


namespace lionheart.Services
{
    public interface IInjuryService
    {
        Task<Result<InjuryDTO, Problem>> CreateInjuryAsync(IdentityUser user, CreateInjuryRequest request);
        Task<Result<InjuryDTO, Problem>> UpdateInjuryAsync(IdentityUser user, UpdateInjuryRequest request);
        Task<Result<Unit, Problem>> DeleteInjuryAsync(IdentityUser user, Guid injuryId);
        Task<Result<InjuryDTO, Problem>> CreateInjuryEventAsync(IdentityUser user, CreateInjuryEventRequest request);
        Task<Result<InjuryDTO, Problem>> UpdateInjuryEventAsync(IdentityUser user, UpdateInjuryEventRequest request);
        Task<Result<Unit, Problem>> DeleteInjuryEventAsync(IdentityUser user, Guid injuryEventId);
        Task<Result<List<InjuryDTO>, Problem>> GetUserInjuriesAsync(IdentityUser user);
    }
    [ToolProvider]
    public class InjuryService : IInjuryService
    {
        private readonly ModelContext _context;

        public InjuryService(ModelContext context)
        {
            _context = context;
        }

        public async Task<Result<InjuryDTO, Problem>> CreateInjuryAsync(IdentityUser user, CreateInjuryRequest request)
        {
            var injury = new Injury
            {
                InjuryID = Guid.NewGuid(),
                UserID = Guid.Parse(user.Id),
                Name = request.Name,
                Notes = request.Notes,
                InjuryDate = DateOnly.FromDateTime(request.InjuryDate),
                IsActive = true,
                InjuryEvents = new()
            };
            _context.Injuries.Add(injury);
            await _context.SaveChangesAsync();
            return Result<InjuryDTO, Problem>.Success(injury.ToDTO());
        }
        public async Task<Result<InjuryDTO, Problem>> UpdateInjuryAsync(IdentityUser user, UpdateInjuryRequest request)
        {
            var userId = Guid.Parse(user.Id);
            var injury = await _context.Injuries
                .Include(i => i.InjuryEvents)
                .FirstOrDefaultAsync(i => i.UserID == userId && i.InjuryID == request.InjuryID);
            if (injury is null) return Result<InjuryDTO, Problem>.Failure(new Problem.NotFound("Injury not found"));
            injury.Name = request.Name;
            injury.Notes = request.Notes;
            injury.IsActive = request.IsActive;
            await _context.SaveChangesAsync();
            return Result<InjuryDTO, Problem>.Success(injury.ToDTO());
        }
        public async Task<Result<InjuryDTO, Problem>> CreateInjuryEventAsync(
            IdentityUser user,
            CreateInjuryEventRequest request)
        {
            var userId = Guid.Parse(user.Id);
            var injury = await _context.Injuries.FirstOrDefaultAsync(i => i.InjuryID == request.InjuryID && i.UserID == userId);
            if (injury is null) return Result<InjuryDTO, Problem>.Failure(new Problem.NotFound("Injury not found"));
            if (request.TrainingSessionID is Guid tsId && tsId != Guid.Empty)
            {
                var ownsSession = await _context.Sessions
                    .AnyAsync(ts => ts.TrainingSessionId == tsId && ts.UserId == userId);
                // Someone else's session is reported as missing, not forbidden: a refusal that
                // distinguishes the two confirms the row exists to a user who cannot read it.
                if (!ownsSession) return Result<InjuryDTO, Problem>.Failure(new Problem.NotFound("Training session not found"));
            }
            // MovementIDs reference MovementData — the reusable movement identity.
            foreach (var movementId in request.MovementIDs)
            {
                var ownsMovement = await _context.MovementDatas
                    .AnyAsync(md => md.MovementDataID == movementId && md.UserID == userId);
                if (!ownsMovement) return Result<InjuryDTO, Problem>.Failure(new Problem.NotFound("One or more movements not found"));
            }

            var newEvent = new InjuryEvent
            {
                InjuryEventID = Guid.NewGuid(),
                InjuryID = injury.InjuryID,
                TrainingSessionID = request.TrainingSessionID,
                Notes = request.Notes,
                PainLevel = request.PainLevel,
                InjuryType = request.InjuryType,
                CreationTime = DateTime.UtcNow,
                MovementIDs = request.MovementIDs
            };
            await _context.InjuryEvents.AddAsync(newEvent);
            await _context.SaveChangesAsync();
            var updatedInjury = await _context.Injuries.Include(i => i.InjuryEvents).FirstAsync(i => i.InjuryID == injury.InjuryID);
            return Result<InjuryDTO, Problem>.Success(updatedInjury.ToDTO());
        }
        [Tool(Name = "GetAllUserInjuries", Description = "Get all injuries for user.")]
        public async Task<Result<List<InjuryDTO>, Problem>> GetUserInjuriesAsync(IdentityUser user)
        {
            var userId = Guid.Parse(user.Id);
            var injuries = await _context.Injuries
                .Where(i => i.UserID == userId)
                .Include(i => i.InjuryEvents)
                .ToListAsync();
            var ordered = injuries
                .OrderByDescending(i => i.InjuryEvents.Count > 0 ? i.InjuryEvents.Max(ie => ie.CreationTime) : i.InjuryDate.ToDateTime(TimeOnly.MinValue))
                .Select(i => i.ToDTO())
                .ToList();
            return Result<List<InjuryDTO>, Problem>.Success(ordered);
        }
        [Tool(Name = "GetUserInjuries", Description = "Get user injuries with filters.")]
        public async Task<Result<List<InjuryDTO>, Problem>> GetUserInjuriesAsync(IdentityUser user, GetInjuryRequest request)
        {
            var userId = Guid.Parse(user.Id);
            var query = _context.Injuries
                .Where(i => i.UserID == userId)
                .Include(i => i.InjuryEvents)
                .AsQueryable();
            if (request.InjuryID.HasValue)
            {
                query = query.Where(i => i.InjuryID == request.InjuryID.Value);
            }
            if (request.IsActive.HasValue)
            {
                query = query.Where(i => i.IsActive == request.IsActive.Value);
            }
            if (request.DateRange is not null)
            {
                var startDate = DateOnly.FromDateTime(request.DateRange.StartDate);
                var endDate = DateOnly.FromDateTime(request.DateRange.EndDate);

                query = query.Where(i => i.InjuryDate >= startDate);


                query = query.Where(i => i.InjuryDate <= endDate);

            }
            var injuries = await query.ToListAsync();
            var ordered = injuries
                .OrderByDescending(i => i.InjuryEvents.Count > 0 ? i.InjuryEvents.Max(ie => ie.CreationTime) : i.InjuryDate.ToDateTime(TimeOnly.MinValue))
                .Select(i => i.ToDTO())
                .ToList();
            return Result<List<InjuryDTO>, Problem>.Success(ordered);
        }

        public async Task<Result<Unit, Problem>> DeleteInjuryAsync(IdentityUser user, Guid injuryId)
        {
            var userId = Guid.Parse(user.Id);
            var injury = await _context.Injuries
                .Include(i => i.InjuryEvents)
                .FirstOrDefaultAsync(i => i.InjuryID == injuryId && i.UserID == userId);
            if (injury is null) return Result<Unit, Problem>.Failure(new Problem.NotFound("Injury not found"));
            _context.InjuryEvents.RemoveRange(injury.InjuryEvents);
            _context.Injuries.Remove(injury);
            await _context.SaveChangesAsync();
            return Result<Unit, Problem>.Success(Unit.Value);
        }

        public async Task<Result<Unit, Problem>> DeleteInjuryEventAsync(IdentityUser user, Guid injuryEventId)
        {
            var userId = Guid.Parse(user.Id);
            var injuryEvent = await _context.InjuryEvents
                .Include(ie => ie.Injury)
                .FirstOrDefaultAsync(ie => ie.InjuryEventID == injuryEventId);
            if (injuryEvent is null) return Result<Unit, Problem>.Failure(new Problem.NotFound("Injury event not found"));
            if (injuryEvent.Injury.UserID != userId) return Result<Unit, Problem>.Failure(new Problem.NotFound("Injury event not found"));
            _context.InjuryEvents.Remove(injuryEvent);
            await _context.SaveChangesAsync();
            return Result<Unit, Problem>.Success(Unit.Value);
        }

        public async Task<Result<InjuryDTO, Problem>> UpdateInjuryEventAsync(IdentityUser user, UpdateInjuryEventRequest request)
        {
            var userId = Guid.Parse(user.Id);
            var injuryEvent = await _context.InjuryEvents
                .Include(ie => ie.Injury)
                .FirstOrDefaultAsync(ie => ie.InjuryEventID == request.InjuryEventID);
            if (injuryEvent is null) return Result<InjuryDTO, Problem>.Failure(new Problem.NotFound("Injury event not found"));
            if (injuryEvent.Injury.UserID != userId) return Result<InjuryDTO, Problem>.Failure(new Problem.NotFound("Injury event not found"));
            injuryEvent.PainLevel = request.PainLevel;
            injuryEvent.InjuryType = request.InjuryType;
            injuryEvent.Notes = request.Notes;
            if (request.TrainingSessionID.HasValue && request.TrainingSessionID.Value != Guid.Empty)
            {
                var tsId = request.TrainingSessionID.Value;
                var ownsSession = await _context.Sessions
                    .AnyAsync(ts => ts.TrainingSessionId == tsId && ts.UserId == userId);
                if (!ownsSession) return Result<InjuryDTO, Problem>.Failure(new Problem.NotFound("Training session not found"));
                injuryEvent.TrainingSessionID = tsId;
            }
            injuryEvent.MovementIDs.Clear();
            foreach (var movementId in request.MovementIDs)
            {
                var ownsMovement = await _context.MovementDatas
                    .AnyAsync(md => md.MovementDataID == movementId && md.UserID == userId);
                if (!ownsMovement) continue;
                injuryEvent.MovementIDs.Add(movementId);
            }
            await _context.SaveChangesAsync();
            var parent = await _context.Injuries
                .Include(i => i.InjuryEvents)
                .FirstAsync(i => i.InjuryID == injuryEvent.InjuryID);
            return Result<InjuryDTO, Problem>.Success(parent.ToDTO());
        }
    }
}