using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Training;
using lionheart.Model.Training.SetEntry;
using lionheart.Services;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using lionheart.Model.Request;
using Model.Chat.Tools;

public interface ITrainingSessionService
{
    /// <summary>
    /// Get all training sessions for a specific program.
    /// </summary>
    /// <remarks>
    /// This does not include child entities. Use <see cref="GetTrainingSessionAsync"/> for that.
    /// </remarks>
    Task<Result<List<TrainingSessionDTO>>> GetTrainingSessionsAsync(IdentityUser user, Guid trainingProgramID);

    /// <summary>
    /// Get all training sessions within <see cref="DateRangeRequest"/> regardless of program association. 
    /// </summary>
    /// <remarks>
    /// This does not include child entities. Use <see cref="GetTrainingSessionAsync"/> for that.
    /// </remarks>
    Task<Result<List<TrainingSessionDTO>>> GetTrainingSessionsAsync(IdentityUser user, DateRangeRequest dateRange);

    /// <summary>
    /// Get a specific training session by ID, including its child entities.
    /// </summary>
    Task<Result<TrainingSessionDTO>> GetTrainingSessionAsync(IdentityUser user, Guid trainingSessionID);

    /// <summary>
    /// Create a new training session.
    /// </summary>
    Task<Result<TrainingSessionDTO>> CreateTrainingSessionAsync(IdentityUser user, CreateTrainingSessionRequest request);

    /// <summary>
    /// Update an existing training session.
    /// </summary>
    Task<Result<TrainingSessionDTO>> UpdateTrainingSessionAsync(IdentityUser user, UpdateTrainingSessionRequest request);


    /// <summary>
    /// Delete a training session.
    /// </summary>
    Task<Result> DeleteTrainingSessionAsync(IdentityUser user, Guid trainingSessionID);


    /// <summary>
    /// Duplicate a training session, deep copying its child entities.
    /// </summary>
    Task<Result<TrainingSessionDTO>> DuplicateTrainingSessionAsync(IdentityUser user, Guid trainingSessionID);
}
[ToolProvider]
public class TrainingSessionService : ITrainingSessionService
{
    private readonly ModelContext _context;
    private readonly IPersonalRecordService _personalRecordService;

    public TrainingSessionService(ModelContext context, IPersonalRecordService personalRecordService)
    {
        _context = context;
        _personalRecordService = personalRecordService;
    }

    [Tool(Name ="GetTrainingSessionsByProgram", Description = "Get all training sessions for a specific training program.")]
    public async Task<Result<List<TrainingSessionDTO>>> GetTrainingSessionsAsync(IdentityUser user, Guid programId)
    {
        var userGuid = Guid.Parse(user.Id);
        var sessions = await _context.TrainingSessions
            .AsNoTracking()
            .Where(ts => ts.TrainingProgramID == programId && ts.UserID == userGuid)
            .OrderBy(ts => ts.Date)
            .ThenBy(ts => ts.CreationTime)
            .ToListAsync();   
        return Result<List<TrainingSessionDTO>>.Success(sessions.Select(s => s.ToDTO()).ToList());
    }
    [Tool(Name ="GetTrainingSessionByID", Description = "Get a specific training session by ID.")]
    public async Task<Result<TrainingSessionDTO>> GetTrainingSessionAsync(IdentityUser user, Guid trainingSessionID)
    {
        var userGuid = Guid.Parse(user.Id);
        var session = await _context.TrainingSessions
            .AsNoTracking()
            .Include(ts => ts.Movements.OrderBy(m => m.Ordering))
                .ThenInclude(m => m.LiftSets)
            .Include(ts => ts.Movements.OrderBy(m => m.Ordering))
                .ThenInclude(m => m.DistanceTimeSets)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.MovementData)
                .ThenInclude(md => md.Equipment)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.MovementData)
                .ThenInclude(md => md.MovementBase)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.MovementData)
                .ThenInclude(md => md.MovementModifier)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == trainingSessionID && ts.UserID == userGuid);

        if (session is null)
        {
            return Result<TrainingSessionDTO>.NotFound("Training session not found or access denied.");
        }
        return Result<TrainingSessionDTO>.Success(session.ToDTO());
    }
    [Tool(Name ="GetTrainingSessionsByDateRange", Description = "Get all training sessions within a specified date range.")]
    public async Task<Result<List<TrainingSessionDTO>>> GetTrainingSessionsAsync(IdentityUser user, DateRangeRequest dateRange)
    {
        var userGuid = Guid.Parse(user.Id);
        var startDate = DateOnly.FromDateTime(dateRange.StartDate);
        var endDate = DateOnly.FromDateTime(dateRange.EndDate);
        var sessions = await _context.TrainingSessions
            .AsNoTracking()
            .Where(ts => ts.UserID == userGuid && ts.Date >= startDate && ts.Date <= endDate)
            .OrderBy(ts => ts.Date)
            .ThenBy(ts => ts.CreationTime)
            .Include(s => s.Movements)
            .ToListAsync();

        return Result<List<TrainingSessionDTO>>.Success(sessions.Select(s => s.ToDTO()).ToList());
    }



    public async Task<Result<TrainingSessionDTO>> CreateTrainingSessionAsync(IdentityUser user, CreateTrainingSessionRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var session = new TrainingSession
        {
            TrainingSessionID = Guid.NewGuid(),
            UserID = userGuid,
            Movements = [],
            Status = TrainingSessionStatus.Planned,
            Date = DateOnly.FromDateTime(request.Date),
            CreationTime = DateTime.UtcNow,
            Notes = request.Notes ?? string.Empty,
            PerceivedEffortRatings = request.PerceivedEffortRatings
        };

        if (request.TrainingProgramID is not null && request.TrainingProgramID != Guid.Empty)
        {
            var program = await _context.TrainingPrograms
                .FirstOrDefaultAsync(tp => tp.TrainingProgramID == request.TrainingProgramID && tp.UserID == userGuid);

            if (program is null)
            {
                return Result<TrainingSessionDTO>.NotFound("Training program not found or access denied.");
            }
            session.TrainingProgramID = program.TrainingProgramID;
        }

        _context.TrainingSessions.Add(session);
        await _context.SaveChangesAsync();
        return Result<TrainingSessionDTO>.Created(session.ToDTO());
    }


    public async Task<Result<TrainingSessionDTO>> UpdateTrainingSessionAsync(IdentityUser user, UpdateTrainingSessionRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var session = await _context.TrainingSessions
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == request.TrainingSessionID && ts.UserID == userGuid);

        if (session is null)
        {
            return Result<TrainingSessionDTO>.NotFound("Training session not found or access denied.");
        }

        var wasNotCompleted = session.Status != TrainingSessionStatus.Completed;
        var becomingCompleted = request.Status == TrainingSessionStatus.Completed;

        if (request.TrainingProgramID is not null && request.TrainingProgramID != Guid.Empty)
        {
            var program = await _context.TrainingPrograms
                .FirstOrDefaultAsync(tp => tp.TrainingProgramID == request.TrainingProgramID && tp.UserID == userGuid);

            if (program is null)
            {
                return Result<TrainingSessionDTO>.NotFound("Training program not found or access denied.");
            }
            session.TrainingProgramID = program.TrainingProgramID;
        }
        else
        {
            session.TrainingProgramID = null;
        }

        session.Date = DateOnly.FromDateTime(request.Date);
        session.Status = request.Status;
        session.Notes = request.Notes;
        session.PerceivedEffortRatings = request.PerceivedEffortRatings is not null
            ? new PerceivedEffortRatings
            {
                RecordedAt = request.PerceivedEffortRatings.RecordedAt,
                AccumulatedFatigue = request.PerceivedEffortRatings.AccumulatedFatigue,
                DifficultyRating = request.PerceivedEffortRatings.DifficultyRating,
                EngagementRating = request.PerceivedEffortRatings.EngagementRating,
                ExternalVariablesRating = request.PerceivedEffortRatings.ExternalVariablesRating
            }
            : null;

        await _context.SaveChangesAsync();

        if (wasNotCompleted && becomingCompleted)
        {
            await _personalRecordService.ProcessTrainingSessionAsync(user, session.TrainingSessionID);
        }

        return Result<TrainingSessionDTO>.Success(session.ToDTO());
    }


    public async Task<Result> DeleteTrainingSessionAsync(IdentityUser user, Guid trainingSessionID)
    {
        var userGuid = Guid.Parse(user.Id);
        var session = await _context.TrainingSessions
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == trainingSessionID && ts.UserID == userGuid);

        if (session is null)
        {
            return Result.NotFound("Training session not found.");
        }

        _context.TrainingSessions.Remove(session);
        await _context.SaveChangesAsync();
        return Result.NoContent();
    }

    //TODO: Better seperate dup logic via having children clone themselves
    public async Task<Result<TrainingSessionDTO>> DuplicateTrainingSessionAsync(IdentityUser user, Guid trainingSessionID)
    {
        var userGuid = Guid.Parse(user.Id);
        var originalSession = await _context.TrainingSessions
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.LiftSets)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.DistanceTimeSets)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == trainingSessionID && ts.UserID == userGuid);

        if (originalSession is null)
        {
            return Result<TrainingSessionDTO>.NotFound("Training session not found or access denied.");
        }

        var newSessionId = Guid.NewGuid();
        var newSession = new TrainingSession
        {
            TrainingSessionID = newSessionId,
            UserID = userGuid,
            TrainingProgramID = originalSession.TrainingProgramID,
            Date = originalSession.Date,
            Status = TrainingSessionStatus.Planned,
            Movements = [],
            CreationTime = DateTime.UtcNow,
            Notes = originalSession.Notes
        };

        foreach (var movement in originalSession.Movements)
        {
            var newMovementId = Guid.NewGuid();
            var newMovement = new Movement
            {
                MovementID = newMovementId,
                TrainingSessionID = newSessionId,
                MovementDataID = movement.MovementDataID,
                Notes = movement.Notes,
                IsCompleted = false,
                Ordering = movement.Ordering,
                LiftSets = [],
                DistanceTimeSets = []
            };

            foreach (var set in movement.LiftSets)
            {
                newMovement.LiftSets.Add(new LiftSetEntry
                {
                    SetEntryID = Guid.NewGuid(),
                    MovementID = newMovementId,
                    RecommendedReps = set.RecommendedReps,
                    RecommendedWeight = set.RecommendedWeight,
                    RecommendedRPE = set.RecommendedRPE,
                    ActualReps = 0,
                    ActualWeight = 0,
                    ActualRPE = 0,
                    WeightUnit = set.WeightUnit
                });
            }

            foreach (var dtSet in movement.DistanceTimeSets)
            {
                newMovement.DistanceTimeSets.Add(new DTSetEntry
                {
                    SetEntryID = Guid.NewGuid(),
                    MovementID = newMovementId,
                    RecommendedDistance = dtSet.RecommendedDistance,
                    ActualDistance = 0,
                    IntervalDuration = dtSet.IntervalDuration,
                    TargetPace = dtSet.TargetPace,
                    ActualPace = TimeSpan.Zero,
                    RecommendedDuration = dtSet.RecommendedDuration,
                    ActualDuration = TimeSpan.Zero,
                    RecommendedRest = dtSet.RecommendedRest,
                    ActualRest = TimeSpan.Zero,
                    IntervalType = dtSet.IntervalType,
                    DistanceUnit = dtSet.DistanceUnit,
                    ActualRPE = 0
                });
            }
            newSession.Movements.Add(newMovement);
        }

        _context.TrainingSessions.Add(newSession);
        await _context.SaveChangesAsync();

        return await GetTrainingSessionAsync(user, newSessionId);
    }


}
