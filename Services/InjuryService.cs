using Ardalis.Result;
using lionheart.Model.InjuryManagement;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using lionheart.Data;
using Model.Chat.Tools;


namespace lionheart.Services
{
    public interface IInjuryService
    {
        Task<Result<InjuryDTO>> CreateInjuryAsync(IdentityUser user, CreateInjuryRequest request);
        Task<Result<InjuryDTO>> UpdateInjuryAsync(IdentityUser user, UpdateInjuryRequest request);
        Task<Result> DeleteInjuryAsync(IdentityUser user, Guid injuryId);
        Task<Result<InjuryDTO>> CreateInjuryEventAsync(IdentityUser user, CreateInjuryEventRequest request);
        Task<Result<InjuryDTO>> UpdateInjuryEventAsync(IdentityUser user, UpdateInjuryEventRequest request);
        Task<Result> DeleteInjuryEventAsync(IdentityUser user, Guid injuryEventId);
        Task<Result<List<InjuryDTO>>> GetUserInjuriesAsync(IdentityUser user);
    }
    [ToolProvider]
    public class InjuryService : IInjuryService
    {
        private readonly ModelContext _context;

        public InjuryService(ModelContext context)
        {
            _context = context;
        }

        public async Task<Result<InjuryDTO>> CreateInjuryAsync(IdentityUser user, CreateInjuryRequest request)
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
            return Result<InjuryDTO>.Created(injury.ToDTO());
        }
        public async Task<Result<InjuryDTO>> UpdateInjuryAsync(IdentityUser user, UpdateInjuryRequest request)
        {
            var userId = Guid.Parse(user.Id);
            var injury = await _context.Injuries
                .Include(i => i.InjuryEvents)
                .FirstOrDefaultAsync(i => i.UserID == userId && i.InjuryID == request.InjuryID);
            if (injury is null) return Result<InjuryDTO>.NotFound("Injury not found");
            injury.Name = request.Name;
            injury.Notes = request.Notes;
            injury.IsActive = request.IsActive;
            await _context.SaveChangesAsync();
            return Result<InjuryDTO>.Success(injury.ToDTO());
        }
        public async Task<Result<InjuryDTO>> CreateInjuryEventAsync(
            IdentityUser user,
            CreateInjuryEventRequest request)
        {
            var userId = Guid.Parse(user.Id);
            var injury = await _context.Injuries.FirstOrDefaultAsync(i => i.InjuryID == request.InjuryID && i.UserID == userId);
            if (injury is null) return Result<InjuryDTO>.NotFound("Injury not found");
            if (request.TrainingSessionID is Guid tsId && tsId != Guid.Empty)
            {
                var ownsSession = await _context.TrainingSessions
                    .AnyAsync(ts => ts.TrainingSessionID == tsId && ts.UserID == userId);
                if (!ownsSession) return Result<InjuryDTO>.Unauthorized("Training session not found or access denied");
            }
            foreach (var movementId in request.MovementIDs)
            {
                var ownsMovement = await _context.Movements
                    .Include(m => m.TrainingSession)
                    .AnyAsync(m => m.MovementID == movementId && m.TrainingSession.UserID == userId);
                if (!ownsMovement) return Result<InjuryDTO>.Unauthorized("One or more movements not found or access denied");
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
            return Result<InjuryDTO>.Success(updatedInjury.ToDTO());
        }
        [Tool(Name = "GetAllUserInjuries", Description = "Get all injuries for user.")]
        public async Task<Result<List<InjuryDTO>>> GetUserInjuriesAsync(IdentityUser user)
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
            return Result<List<InjuryDTO>>.Success(ordered);
        }
        [Tool(Name = "GetUserInjuries", Description = "Get user injuries with filters.")]
        public async Task<Result<List<InjuryDTO>>> GetUserInjuriesAsync(IdentityUser user, GetInjuryRequest request)
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
            return Result<List<InjuryDTO>>.Success(ordered);
        }

        public async Task<Result> DeleteInjuryAsync(IdentityUser user, Guid injuryId)
        {
            var userId = Guid.Parse(user.Id);
            var injury = await _context.Injuries
                .Include(i => i.InjuryEvents)
                .FirstOrDefaultAsync(i => i.InjuryID == injuryId && i.UserID == userId);
            if (injury is null) return Result.NotFound("Injury not found");
            _context.InjuryEvents.RemoveRange(injury.InjuryEvents);
            _context.Injuries.Remove(injury);
            await _context.SaveChangesAsync();
            return Result.Success();
        }

        public async Task<Result> DeleteInjuryEventAsync(IdentityUser user, Guid injuryEventId)
        {
            var userId = Guid.Parse(user.Id);
            var injuryEvent = await _context.InjuryEvents
                .Include(ie => ie.Injury)
                .FirstOrDefaultAsync(ie => ie.InjuryEventID == injuryEventId);
            if (injuryEvent is null) return Result.NotFound("Injury event not found");
            if (injuryEvent.Injury.UserID != userId) return Result.Unauthorized("You do not have permission to delete this injury event");
            _context.InjuryEvents.Remove(injuryEvent);
            await _context.SaveChangesAsync();
            return Result.Success();
        }

        public async Task<Result<InjuryDTO>> UpdateInjuryEventAsync(IdentityUser user, UpdateInjuryEventRequest request)
        {
            var userId = Guid.Parse(user.Id);
            var injuryEvent = await _context.InjuryEvents
                .Include(ie => ie.Injury)
                .FirstOrDefaultAsync(ie => ie.InjuryEventID == request.InjuryEventID);
            if (injuryEvent is null) return Result<InjuryDTO>.NotFound("Injury event not found");
            if (injuryEvent.Injury.UserID != userId) return Result<InjuryDTO>.Unauthorized();
            injuryEvent.PainLevel = request.PainLevel;
            injuryEvent.InjuryType = request.InjuryType;
            injuryEvent.Notes = request.Notes;
            if (request.TrainingSessionID.HasValue && request.TrainingSessionID.Value != Guid.Empty)
            {
                var tsId = request.TrainingSessionID.Value;
                var ownsSession = await _context.TrainingSessions
                    .AnyAsync(ts => ts.TrainingSessionID == tsId && ts.UserID == userId);
                if (!ownsSession) return Result<InjuryDTO>.Unauthorized("Training session not found or access denied");
                injuryEvent.TrainingSessionID = tsId;
            }
            injuryEvent.MovementIDs.Clear();
            foreach (var movementId in request.MovementIDs)
            {
                var ownsMovement = await _context.Movements
                    .Include(m => m.TrainingSession)
                    .AnyAsync(m => m.MovementID == movementId && m.TrainingSession.UserID == userId);
                if (!ownsMovement) continue;
                injuryEvent.MovementIDs.Add(movementId);
            }
            await _context.SaveChangesAsync();
            var parent = await _context.Injuries
                .Include(i => i.InjuryEvents)
                .FirstAsync(i => i.InjuryID == injuryEvent.InjuryID);
            return Result<InjuryDTO>.Success(parent.ToDTO());
        }
    }
}