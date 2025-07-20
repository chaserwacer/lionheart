using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using lionheart.Services;
using System.ComponentModel;

[McpServerToolType]
public class TrainingSessionService : ITrainingSessionService
{
    private readonly ModelContext _context;


    private class AiSession
    {
        public DateOnly Date { get; set; }

    }

    // private class AiMovement
    // {
    //     public Guid MovementBaseID { get; set; }
    //     public MovementModifier Modifier { get; set; } = new MovementModifier();
    //     public int Reps { get; set; }
    //     public double Weight { get; set; }
    //     public double RPE { get; set; }
    //     public WeightUnit Unit { get; set; }
    //     public string Notes { get; set; } = string.Empty;
    //     public int Ordering { get; set; }
    // }

    public TrainingSessionService(ModelContext context)
    {
        _context = context;
    }
    private async Task<List<TrainingSessionDTO>> GetOrderedSessionsAsync(Guid programId, Guid userGuid)
    {
        var orderedSessions = await _context.TrainingSessions
            .Where(ts => ts.TrainingProgramID == programId &&
                        ts.TrainingProgram!.UserID == userGuid)
            .Include(ts => ts.TrainingProgram)
            .Include(ts => ts.Movements.OrderBy(m => m.Ordering))
                .ThenInclude(m => m.Sets)
            .Include(ts => ts.Movements)
                .ThenInclude(m => m.MovementBase)
            .Include(m => m.Movements)
                .ThenInclude(m => m.MovementModifier.Equipment)
            .OrderBy(ts => ts.Date)
            .ThenBy(ts => ts.CreationTime)
            .ToListAsync();

        int sessionIndex = 1;
        var sessions = new List<TrainingSessionDTO>();
        foreach (var group in orderedSessions.GroupBy(s => s.Date))
        {
            var sameDaySessions = group.OrderBy(s => s.CreationTime).ToList();
            for (int i = 0; i < sameDaySessions.Count; i++)
            {

                var sessionNumberStr = $"{sessionIndex}.{i:D2}";
                var sessionNumber = double.Parse(sessionNumberStr);
                sessions.Add(sameDaySessions[i].ToDTO(sessionNumber));
            }
            sessionIndex++;
        }
        return sessions;
    }


    [McpServerTool, Description("Get all training sessions for a program.")]
    public async Task<Result<List<TrainingSessionDTO>>> GetTrainingSessionsAsync(IdentityUser user, Guid programId)
    {
        var userGuid = Guid.Parse(user.Id);

        return Result<List<TrainingSessionDTO>>.Success(await GetOrderedSessionsAsync(programId, userGuid));
    }

    [McpServerTool, Description("Get a specific training session by ID.")]
    public async Task<Result<TrainingSessionDTO>> GetTrainingSessionAsync(IdentityUser user, GetTrainingSessionRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var sessions = await GetOrderedSessionsAsync(request.TrainingProgramID, userGuid);
        var session = sessions.First(s => s.TrainingSessionID == request.TrainingSessionID);
        if (session is null)
        {
            return Result<TrainingSessionDTO>.NotFound("Training session not found.");
        }
        return Result<TrainingSessionDTO>.Success(session);
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
            TrainingProgramID = request.TrainingProgramID,
            Status = TrainingSessionStatus.Planned,
            Date = date,
            CreationTime = DateTime.UtcNow // <-- Set creation time
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

        return Result<TrainingSessionDTO>.Created(session.ToDTO(sessionNumber));
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

        return Result<TrainingSessionDTO>.Success(hydratedSession.ToDTO(sessionNumber));
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


    [McpServerTool, Description("create new session with movements.")]
    public async Task<Result<TrainingSessionDTO>> CreateTrainingSessionFromJSON(
            IdentityUser user,
            TrainingSessionDTO trainingSessionDTO)
    {
        // 1. Validate program exists
        var programId = trainingSessionDTO.TrainingProgramID;
        if (!await _context.TrainingPrograms.AnyAsync(p => p.TrainingProgramID == programId))
            return Result<TrainingSessionDTO>.Error("Invalid TrainingProgramID.");

        // 2) Create root session
        var newSession = new TrainingSession
        {
            TrainingSessionID = Guid.NewGuid(),
            TrainingProgramID = trainingSessionDTO.TrainingProgramID,
            Date = trainingSessionDTO.Date,
            Status = trainingSessionDTO.Status
        };

        int order = 0;
        foreach (var mDto in trainingSessionDTO.Movements)
        {
            // fetch the actual MovementBase entity so nav-prop is populated
            var baseEntity = await _context.MovementBases.FindAsync(mDto.MovementBaseID)!;
            if (baseEntity is null)
            {
                return Result<TrainingSessionDTO>.Error($"MovementBaseID {mDto.MovementBaseID} not found.");
            }

            var newMovement = new Movement
            {
                MovementID = Guid.NewGuid(),
                TrainingSessionID = newSession.TrainingSessionID,
                MovementBaseID = mDto.MovementBaseID,
                MovementBase = baseEntity,            // ← set nav-prop
                Notes = mDto.Notes,
                MovementModifier = mDto.MovementModifier, // ← copy modifier
                IsCompleted = mDto.IsCompleted,      // ← copy completion
                Ordering = order++
            };

            foreach (var sDto in mDto.Sets)
            {
                var newSet = new SetEntry
                {
                    SetEntryID = Guid.NewGuid(),
                    MovementID = newMovement.MovementID, // ← explicit FK
                    RecommendedReps = sDto.RecommendedReps,
                    RecommendedWeight = sDto.RecommendedWeight,
                    RecommendedRPE = sDto.RecommendedRPE,
                    ActualReps = sDto.ActualReps,
                    ActualWeight = sDto.ActualWeight,
                    ActualRPE = sDto.ActualRPE
                };
                newMovement.Sets.Add(newSet);
            }

            newSession.Movements.Add(newMovement);
        }

        // 3) Persist
        await _context.TrainingSessions.AddAsync(newSession);
        await _context.SaveChangesAsync();

        // 4) Reload with nav-props so ToDTO() can see names & modifiers
        var sessionWithNav = await _context.TrainingSessions
        .AsNoTracking()
        .Include(ts => ts.Movements.OrderBy(m => m.Ordering))!
            .ThenInclude(m => m.MovementBase)
        .Include(ts => ts.Movements)!
            .ThenInclude(m => m.MovementModifier)
        .Include(ts => ts.Movements)!
            .ThenInclude(m => m.Sets)
        .FirstAsync(ts => ts.TrainingSessionID == newSession.TrainingSessionID);

        // 5) Return fully hydrated DTO
        return Result<TrainingSessionDTO>.Created(
        sessionWithNav.ToDTO(trainingSessionDTO.SessionNumber));
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
            CreationTime = DateTime.UtcNow 
        };

        foreach (var movement in originalSession.Movements)
        {
            var newMovementModififer = new MovementModifier()
            {
                Name = movement.MovementModifier.Name,
                Equipment = movement.MovementModifier.Equipment,
                Duration = movement.MovementModifier.Duration,
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
                WeightUnit = movement.WeightUnit,
                Sets = new List<SetEntry>()
            };

            foreach (var set in movement.Sets)
            {
                var newSet = new SetEntry
                {
                    SetEntryID = Guid.NewGuid(),
                    MovementID = newMovement.MovementID,
                    Movement = newMovement,
                    RecommendedReps = set.RecommendedReps,
                    RecommendedWeight = set.RecommendedWeight,
                    RecommendedRPE = set.RecommendedRPE,
                    ActualReps = set.ActualReps,
                    ActualWeight = set.ActualWeight,
                    ActualRPE = set.ActualRPE
                };
                newMovement.Sets.Add(newSet);
            }
            newSession.Movements.Add(newMovement);
        }

        _context.TrainingSessions.Add(newSession);
        await _context.SaveChangesAsync();

        var sessions = await GetOrderedSessionsAsync(newSession.TrainingProgramID, userGuid);
        var session = sessions.First(s => s.TrainingSessionID == newSession.TrainingSessionID);
        if (session is null)
        {
            return Result<TrainingSessionDTO>.Error("Error duplicating session.");
        }
        return Result<TrainingSessionDTO>.Success(session);
    }
}