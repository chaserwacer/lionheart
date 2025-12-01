using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using lionheart.Services;
using System.ComponentModel;
using Mapster;
using lionheart.Model.TrainingProgram.SetEntry;

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

[McpServerToolType]
public class TrainingSessionService : ITrainingSessionService
{
    private readonly ModelContext _context;



    public TrainingSessionService(ModelContext context)
    {
        _context = context;
    }


    [McpServerTool, Description("Get all training sessions for a program.")]
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


    [McpServerTool, Description("Get a specific training session by ID.")]
    public async Task<Result<TrainingSessionDTO>> GetTrainingSessionAsync(IdentityUser user, Guid trainingSessionID)
    {
        var userGuid = Guid.Parse(user.Id);
        var session = await _context.TrainingSessions
            .Include(ts => ts.TrainingProgram)
            .Include(ts => ts.Movements.OrderBy(m => m.Ordering))
                .ThenInclude(m => m.Sets)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.MovementBase)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.MovementModifier.Equipment)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == trainingSessionID &&
                                      ts.TrainingProgram!.UserID == userGuid);
        if (session is null)
        {
            return Result<TrainingSessionDTO>.NotFound("Training session not found or access denied.");
        }
        return Result<TrainingSessionDTO>.Success(session.Adapt<TrainingSessionDTO>());
    }


    [McpServerTool, Description("Create a new training session.")]
    public async Task<Result<TrainingSessionDTO>> CreateTrainingSessionAsync(IdentityUser user, CreateTrainingSessionRequest request)
    {
        var userGuid = Guid.Parse(user.Id);

        // Verify user owns the training program
        var program = await _context.TrainingPrograms
            .FirstOrDefaultAsync(tp => tp.TrainingProgramID == request.TrainingProgramID &&
                                      tp.UserID == userGuid);

        if (program is null)
        {
            return Result<TrainingSessionDTO>.NotFound("Training program not found or access denied.");
        }


        var date = request.Date;
        var session = new TrainingSession
        {
            TrainingSessionID = Guid.NewGuid(),
            Movements = new List<Movement>(),
            TrainingProgramID = request.TrainingProgramID,
            Status = TrainingSessionStatus.Planned,
            Date = date,
            CreationTime = DateTime.UtcNow, // <-- Set creation time
            Notes = string.Empty
        };

        _context.TrainingSessions.Add(session);
        await _context.SaveChangesAsync();

        // Calculate session number for the newly created session
        var sessionNumber = await _context.TrainingSessions
            .Where(ts => ts.TrainingProgramID == request.TrainingProgramID &&
                        ts.Date <= date)
            .OrderBy(ts => ts.Date)
            .ThenBy(ts => ts.CreationTime)
            .CountAsync(ts => ts.Date < date ||
                            (ts.Date == date && ts.TrainingSessionID.CompareTo(session.TrainingSessionID) <= 0));

        return Result<TrainingSessionDTO>.Created(session.Adapt<TrainingSessionDTO>());
    }


    [McpServerTool, Description("Update an existing training session.")]
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

        session.Date = request.Date;
        session.Status = request.Status;
        session.Notes = request.Notes;

        await _context.SaveChangesAsync();

        // Recalculate session number after update
        var sessionNumber = await _context.TrainingSessions
            .Where(ts => ts.TrainingProgramID == session.TrainingProgramID &&
                        ts.Date <= session.Date)
            .OrderBy(ts => ts.Date)
            .ThenBy(ts => ts.TrainingSessionID)
            .CountAsync(ts => ts.Date < session.Date ||
                            (ts.Date == session.Date && ts.TrainingSessionID.CompareTo(session.TrainingSessionID) <= 0));

        // Reload the session with all navigation properties
        var hydratedSession = await _context.TrainingSessions
            .AsNoTracking()
            .Include(ts => ts.TrainingProgram)
            .Include(ts => ts.Movements.OrderBy(m => m.Ordering))
                .ThenInclude(m => m.Sets)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.MovementBase)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.MovementModifier.Equipment)
            .FirstOrDefaultAsync(ts => ts.TrainingSessionID == session.TrainingSessionID);

        if (hydratedSession is null)
        {
            return Result<TrainingSessionDTO>.NotFound("Training session not found after update.");
        }

        return Result<TrainingSessionDTO>.Success(hydratedSession.Adapt<TrainingSessionDTO>());
    }


    [McpServerTool, Description("Delete a training session.")]
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


    [McpServerTool, Description("Duplicate an existing training session.")]
    public async Task<Result<TrainingSessionDTO>> DuplicateTrainingSessionAsync(IdentityUser user, Guid trainingSessionID)
    {
        var userGuid = Guid.Parse(user.Id);
        var originalSession = await _context.TrainingSessions
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.Sets)
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
            Movements = new List<Movement>(),
            CreationTime = DateTime.UtcNow,
            Notes = originalSession.Notes
        };

        foreach (var movement in originalSession.Movements)
        {
            var equipment = await _context.Equipments.FindAsync(movement.MovementModifier.EquipmentID);
            if (equipment is null)
            {
                return Result<TrainingSessionDTO>.Error($"EquipmentID {movement.MovementModifier.EquipmentID} not found.");
            }
            var newMovementModififer = new MovementModifier()
            {
                Name = movement.MovementModifier.Name,
                EquipmentID = movement.MovementModifier.EquipmentID,
                Equipment = equipment,
            };
            var movementBase = await _context.MovementBases.FindAsync(movement.MovementBaseID);
            if (movementBase is null)
            {
                return Result<TrainingSessionDTO>.Error($"MovementBaseID {movement.MovementBaseID} not found.");
            }
            var newMovement = new Movement
            {
                MovementID = Guid.NewGuid(),
                TrainingSessionID = newSession.TrainingSessionID,
                TrainingSession = newSession,
                MovementBaseID = movementBase.MovementBaseID,
                MovementBase = movementBase,
                MovementModifier = newMovementModififer,
                Notes = movement.Notes,
                IsCompleted = false,
                Ordering = movement.Ordering,
                Sets = new List<ISetEntry>()
            };

            foreach (var set in movement.Sets)
            {
                ISetEntry newSet;
                
                if (set is LiftSetEntry liftSet)
                {
                    newSet = new LiftSetEntry
                    {
                        SetEntryID = Guid.NewGuid(),
                        MovementID = newMovement.MovementID,
                        Movement = newMovement,
                        RecommendedReps = liftSet.RecommendedReps,
                        RecommendedWeight = liftSet.RecommendedWeight,
                        RecommendedRPE = liftSet.RecommendedRPE,
                        ActualReps = liftSet.ActualReps,
                        ActualWeight = liftSet.ActualWeight,
                        ActualRPE = liftSet.ActualRPE,
                        WeightUnit = liftSet.WeightUnit
                    };
                }
                else if (set is DTSetEntry dtSet)
                {
                    newSet = new DTSetEntry
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
                    };
                }
                else
                {
                    return Result<TrainingSessionDTO>.Error($"Unknown set entry type: {set.GetType().Name}");
                }
                
                newMovement.Sets.Add(newSet);
            }
            newSession.Movements.Add(newMovement);
        }

        _context.TrainingSessions.Add(newSession);
        await _context.SaveChangesAsync();

        return Result<TrainingSessionDTO>.Success(newSession.Adapt<TrainingSessionDTO>());
    }

}