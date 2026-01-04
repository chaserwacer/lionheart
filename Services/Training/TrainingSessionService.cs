using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.DTOs;
using lionheart.Model.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using lionheart.Services;
using System.ComponentModel;
using Mapster;
using lionheart.Model.Training.SetEntry;

public interface ITrainingSessionService
{
    /// <summary>
    /// Get all training sessions for a specific program.
    /// </summary>
    /// <param name="user">The user who owns the program.</param>
    /// <param name="trainingProgramID">The program ID to get sessions for.</param>
    /// <returns>A result containing a list of training sessions.</returns>
    Task<Result<List<TrainingSessionDTO>>> GetTrainingSessionsAsync(IdentityUser user, Guid trainingProgramID);

    /// <summary>
    /// Get a specific training session by ID.
    /// </summary>
    /// <param name="user">The user who owns the session.</param>
    /// <param name="trainingSessionID">The session ID to retrieve.</param>
    /// <returns>A result containing the training session.</returns>
    Task<Result<TrainingSessionDTO>> GetTrainingSessionAsync(IdentityUser user, Guid trainingSessionID);

    /// <summary>
    /// Create a new training session within a program.
    /// </summary>
    /// <param name="user">The user to create the session for.</param>
    /// <param name="programId">The program ID to add the session to.</param>
    /// <param name="request">The session creation request.</param>
    /// <returns>A result containing the created session.</returns>
    Task<Result<TrainingSessionDTO>> CreateTrainingSessionAsync(IdentityUser user, CreateTrainingSessionRequest request);

    /// <summary>
    /// Update an existing training session.
    /// </summary>
    /// <param name="user">The user who owns the session.</param>
    /// <param name="sessionId">The session ID to update.</param>
    /// <param name="request">The session update request.</param>
    /// <returns>A result containing the updated session.</returns>
    Task<Result<TrainingSessionDTO>> UpdateTrainingSessionAsync(IdentityUser user, UpdateTrainingSessionRequest request);


    /// <summary>
    /// Delete a training session.
    /// </summary>
    /// <param name="user">The user who owns the session.</param>
    /// <param name="sessionId">The session ID to delete.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> DeleteTrainingSessionAsync(IdentityUser user, Guid trainingSessionID);


    /// <summary>
    /// Duplicate a training session, including all movements and set entries.
    /// </summary>
    /// <param name="user">The user who owns the session.</param>
    /// <param name="trainingSessionID">The session ID to duplicate.</param>
    /// <returns>A result containing the duplicated training session.</returns>
    Task<Result<TrainingSessionDTO>> DuplicateTrainingSessionAsync(IdentityUser user, Guid trainingSessionID);
}

public class TrainingSessionService : ITrainingSessionService
{
    private readonly ModelContext _context;
    private readonly IPersonalRecordService _personalRecordService;

    public TrainingSessionService(ModelContext context, IPersonalRecordService personalRecordService)
    {
        _context = context;
        _personalRecordService = personalRecordService;
    }


    public async Task<Result<List<TrainingSessionDTO>>> GetTrainingSessionsAsync(IdentityUser user, Guid programId)
    {
        var userGuid = Guid.Parse(user.Id);
        var sessions = await _context.TrainingSessions
            .Include(ts => ts.TrainingProgram)
            .Where(ts => ts.TrainingProgramID == programId &&
                         ts.TrainingProgram!.UserID == userGuid)
            .OrderBy(ts => ts.Date)
            .ThenBy(ts => ts.CreationTime)
            .ProjectToType<TrainingSessionDTO>()
            .ToListAsync();
        return Result<List<TrainingSessionDTO>>.Success(sessions);

    }

    public async Task<Result<TrainingSessionDTO>> GetTrainingSessionAsync(IdentityUser user, Guid trainingSessionID)
    {
        var userGuid = Guid.Parse(user.Id);
        var session = await _context.TrainingSessions
            .Include(ts => ts.TrainingProgram)
             .Include(ts => ts.Movements.OrderBy(m => m.Ordering))
                .ThenInclude(m => m.LiftSets)
            .Include(ts => ts.Movements.OrderBy(m => m.Ordering))
                .ThenInclude(m => m.DistanceTimeSets)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.MovementData)
                .ThenInclude(md => md.Equipment)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == trainingSessionID &&
                                      ts.TrainingProgram!.UserID == userGuid);
        if (session is null)
        {
            return Result<TrainingSessionDTO>.NotFound("Training session not found or access denied.");
        }
        return Result<TrainingSessionDTO>.Success(session.Adapt<TrainingSessionDTO>());
    }


    public async Task<Result<TrainingSessionDTO>> CreateTrainingSessionAsync(IdentityUser user, CreateTrainingSessionRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var date = request.Date;
        var session = new TrainingSession
        {
            TrainingSessionID = Guid.NewGuid(),
            Movements = new List<Movement>(),
            Status = TrainingSessionStatus.Planned,
            Date = date,
            CreationTime = DateTime.UtcNow,
            Notes = string.Empty,
            PerceivedEffortRatings = request.PerceivedEffortRatings
        };

        if (request.TrainingProgramID is not null && request.TrainingProgramID != Guid.Empty)
        {
            var program = await _context.TrainingPrograms
            .FirstOrDefaultAsync(tp => tp.TrainingProgramID == request.TrainingProgramID &&
                                      tp.UserID == userGuid);

            if (program is null)
            {
                return Result<TrainingSessionDTO>.NotFound("Training program not found or access denied.");
            }
            session.TrainingProgramID = program.TrainingProgramID;
        }

        _context.TrainingSessions.Add(session);
        await _context.SaveChangesAsync();

        return Result<TrainingSessionDTO>.Created(session.Adapt<TrainingSessionDTO>());
    }


    public async Task<Result<TrainingSessionDTO>> UpdateTrainingSessionAsync(IdentityUser user, UpdateTrainingSessionRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var session = await _context.TrainingSessions
            .Include(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(ts =>
                ts.TrainingSessionID == request.TrainingSessionID &&
                ts.TrainingProgram!.UserID == userGuid);

        if (session is null)
        {
            return Result<TrainingSessionDTO>.NotFound("Training session not found or access denied.");
        }

        // Track if status is changing to Completed for PR processing
        var wasNotCompleted = session.Status != TrainingSessionStatus.Completed;
        var becomingCompleted = request.Status == TrainingSessionStatus.Completed;

        if (request.TrainingProgramID is not null && request.TrainingProgramID != Guid.Empty)
        {
            var program = await _context.TrainingPrograms
            .FirstOrDefaultAsync(tp => tp.TrainingProgramID == request.TrainingProgramID &&
                                      tp.UserID == userGuid);

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
        session.Date = request.Date;
        session.Status = request.Status;
        session.Notes = request.Notes;
        if (request.PerceivedEffortRatings is not null)
        {
            session.PerceivedEffortRatings = new PerceivedEffortRatings
            {
                AccumulatedFatigue = request.PerceivedEffortRatings.AccumulatedFatigue,
                DifficultyRating = request.PerceivedEffortRatings.DifficultyRating,
                EngagementRating = request.PerceivedEffortRatings.EngagementRating,
                ExternalVariablesRating = request.PerceivedEffortRatings.ExternalVariablesRating
            };
        }
        else
        {
            session.PerceivedEffortRatings = null;
        }
        await _context.SaveChangesAsync();

        // Process PRs when session is marked as Completed
        if (wasNotCompleted && becomingCompleted)
        {
            await _personalRecordService.ProcessTrainingSessionAsync(user, session.TrainingSessionID);
        }

        return Result<TrainingSessionDTO>.Success(session.Adapt<TrainingSessionDTO>());
    }


    public async Task<Result> DeleteTrainingSessionAsync(IdentityUser user, Guid trainingSessionID)
    {
        var userGuid = Guid.Parse(user.Id);
        var session = await _context.TrainingSessions
            .Include(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == trainingSessionID &&
                                      ts.TrainingProgram!.UserID == userGuid);

        if (session is null)
        {
            return Result.NotFound("Training session not found or access denied.");
        }

        _context.TrainingSessions.Remove(session);
        await _context.SaveChangesAsync();
        return Result.NoContent();
    }


    public async Task<Result<TrainingSessionDTO>> DuplicateTrainingSessionAsync(IdentityUser user, Guid trainingSessionID)
    {
        var userGuid = Guid.Parse(user.Id);
        var originalSession = await _context.TrainingSessions
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.LiftSets)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.DistanceTimeSets)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.MovementData)
            .Include(ts => ts.TrainingProgram)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == trainingSessionID && ts.TrainingProgram!.UserID == userGuid);

        if (originalSession == null)
            return Result.NotFound();

        // Create new session
        var newSession = new TrainingSession
        {
            TrainingSessionID = Guid.NewGuid(),
            TrainingProgramID = originalSession.TrainingProgramID,
            TrainingProgram = originalSession.TrainingProgram,
            Date = originalSession.Date,
            Status = TrainingSessionStatus.Planned,
            Movements = [],
            CreationTime = DateTime.UtcNow,
            Notes = originalSession.Notes
        };

        foreach (var movement in originalSession.Movements)
        {
            // Reuse the same MovementData since it's now a shared reference
            var newMovement = new Movement
            {
                MovementID = Guid.NewGuid(),
                TrainingSessionID = newSession.TrainingSessionID,
                TrainingSession = newSession,
                MovementDataID = movement.MovementDataID,
                MovementData = movement.MovementData,
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
                    MovementID = newMovement.MovementID,
                    Movement = newMovement,
                    RecommendedReps = set.RecommendedReps,
                    RecommendedWeight = set.RecommendedWeight,
                    RecommendedRPE = set.RecommendedRPE,
                    ActualReps = set.ActualReps,
                    ActualWeight = set.ActualWeight,
                    ActualRPE = set.ActualRPE,
                    WeightUnit = set.WeightUnit
                });
            }

            foreach (var dtSet in movement.DistanceTimeSets)
            {
                newMovement.DistanceTimeSets.Add(new DTSetEntry
                {
                    SetEntryID = Guid.NewGuid(),
                    MovementID = newMovement.MovementID,
                    Movement = newMovement,
                    RecommendedDistance = dtSet.RecommendedDistance,
                    ActualDistance = dtSet.ActualDistance,
                    IntervalDuration = dtSet.IntervalDuration,
                    TargetPace = dtSet.TargetPace,
                    ActualPace = dtSet.ActualPace,
                    RecommendedDuration = dtSet.RecommendedDuration,
                    ActualDuration = dtSet.ActualDuration,
                    RecommendedRest = dtSet.RecommendedRest,
                    ActualRest = dtSet.ActualRest,
                    IntervalType = dtSet.IntervalType,
                    DistanceUnit = dtSet.DistanceUnit,
                    ActualRPE = dtSet.ActualRPE
                });
            }
            newSession.Movements.Add(newMovement);
        }

        _context.TrainingSessions.Add(newSession);
        await _context.SaveChangesAsync();

        return Result<TrainingSessionDTO>.Success(newSession.Adapt<TrainingSessionDTO>());
    }

}
