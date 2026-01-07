using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Training.SetEntry;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services
{
    public class DTSetEntryService : IDTSetEntryService
    {
        private readonly ModelContext _context;

        public DTSetEntryService(ModelContext context)
        {
            _context = context;
        }

        public async Task<Result<DTSetEntryDTO>> CreateDTSetEntryAsync(IdentityUser user, CreateDTSetEntryRequest request)
        {
            var userGuid = Guid.Parse(user.Id);

            var movement = await _context.Movements
                .Include(m => m.TrainingSession)
                .FirstOrDefaultAsync(m => m.MovementID == request.MovementID && m.TrainingSession.UserID == userGuid);

            if (movement is null)
            {
                return Result<DTSetEntryDTO>.NotFound("Movement not found or access denied.");
            }

            var setEntry = new DTSetEntry
            {
                SetEntryID = Guid.NewGuid(),
                MovementID = request.MovementID,
                Movement = movement,
                RecommendedDistance = request.RecommendedDistance,
                ActualDistance = request.ActualDistance,
                IntervalDuration = request.IntervalDuration,
                TargetPace = request.TargetPace,
                ActualPace = request.ActualPace,
                RecommendedDuration = request.RecommendedDuration,
                ActualDuration = request.ActualDuration,
                RecommendedRest = request.RecommendedRest,
                ActualRest = request.ActualRest,
                IntervalType = request.IntervalType,
                DistanceUnit = request.DistanceUnit,
                ActualRPE = request.ActualRPE
            };

            _context.DTSetEntries.Add(setEntry);
            await _context.SaveChangesAsync();

            return Result<DTSetEntryDTO>.Created(setEntry.Adapt<DTSetEntryDTO>());
        }

        public async Task<Result<DTSetEntryDTO>> UpdateDTSetEntryAsync(IdentityUser user, UpdateDTSetEntryRequest request)
        {
            var userGuid = Guid.Parse(user.Id);
            var setEntry = await _context.DTSetEntries
                .Include(se => se.Movement)
                .ThenInclude(m => m.TrainingSession)
                .FirstOrDefaultAsync(se => se.SetEntryID == request.SetEntryID);

            if (setEntry is null || setEntry.Movement.TrainingSession.UserID != userGuid)
            {
                return Result<DTSetEntryDTO>.NotFound("Set entry not found or access denied.");
            }

            setEntry.RecommendedDistance = request.RecommendedDistance;
            setEntry.ActualDistance = request.ActualDistance;
            setEntry.IntervalDuration = request.IntervalDuration;
            setEntry.TargetPace = request.TargetPace;
            setEntry.ActualPace = request.ActualPace;
            setEntry.RecommendedDuration = request.RecommendedDuration;
            setEntry.ActualDuration = request.ActualDuration;
            setEntry.RecommendedRest = request.RecommendedRest;
            setEntry.ActualRest = request.ActualRest;
            setEntry.IntervalType = request.IntervalType;
            setEntry.DistanceUnit = request.DistanceUnit;
            setEntry.ActualRPE = request.ActualRPE;

            await _context.SaveChangesAsync();

            return Result<DTSetEntryDTO>.Success(setEntry.Adapt<DTSetEntryDTO>());
        }

        public async Task<Result> DeleteDTSetEntryAsync(IdentityUser user, Guid setEntryId)
        {
            var userGuid = Guid.Parse(user.Id);
            var setEntry = await _context.DTSetEntries
                .Include(se => se.Movement)
                .ThenInclude(m => m.TrainingSession)
                .FirstOrDefaultAsync(se => se.SetEntryID == setEntryId);

            if (setEntry is null || setEntry.Movement.TrainingSession.UserID != userGuid)
            {
                return Result.NotFound("Set entry not found or access denied.");
            }

            _context.DTSetEntries.Remove(setEntry);
            await _context.SaveChangesAsync();
            return Result.NoContent();
        }
    }
}
