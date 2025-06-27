using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;            // for JsonSerializer
using Model.McpServer;             // for LionMcpPrompt, InstructionPromptSection
using ModelContextProtocol.Server;
using lionheart.Services;
using System.ComponentModel;

[McpServerToolType]
public class TrainingSessionService : ITrainingSessionService
{
    private readonly ModelContext _context;
    private readonly IMCPClientService _ai;
    private readonly ILogger<MCPClientService> _logger;

    private class AiSession
    {
        public DateOnly Date { get; set; }

    }

    private class AiMovement
    {
        public Guid MovementBaseID { get; set; }
        public MovementModifier Modifier { get; set; } = new MovementModifier();
        public int Reps { get; set; }
        public double Weight { get; set; }
        public double RPE { get; set; }
        public WeightUnit Unit { get; set; }
        public string Notes { get; set; } = string.Empty;
        public int Ordering { get; set; }
    }

    public TrainingSessionService(ModelContext context, IMCPClientService aiClient, ILogger<MCPClientService> logger)
    {
        _logger = logger;
        _context = context;
        _ai = aiClient;
    }

    [McpServerTool, Description("Get all training sessions for a program.")]
    public async Task<Result<List<TrainingSessionDTO>>> GetTrainingSessionsAsync(IdentityUser user, Guid programId)
    {
        var userGuid = Guid.Parse(user.Id);

        var sessions = await _context.TrainingSessions
            .Where(ts => ts.TrainingProgramID == programId &&
                        ts.TrainingProgram!.UserID == userGuid)
            .Include(ts => ts.TrainingProgram)
            .Include(ts => ts.Movements)
            .ThenInclude(m => m.Sets)
            .Include(ts => ts.Movements)
            .ThenInclude(m => m.MovementBase)
            .OrderBy(ts => ts.Date)
            .ToListAsync();

        // Generate session numbers based on date order within the program
        var sessionDTOs = sessions.Select((session, index) => session.ToDTO(index + 1)).ToList();

        return Result<List<TrainingSessionDTO>>.Success(sessionDTOs);
    }

    [McpServerTool, Description("Get a specific training session by ID.")]
    public async Task<Result<TrainingSessionDTO>> GetTrainingSessionAsync(IdentityUser user, Guid trainingSessionID)
    {
        var userGuid = Guid.Parse(user.Id);
        var session = await _context.TrainingSessions
            .Where(ts => ts.TrainingSessionID == trainingSessionID &&
                        ts.TrainingProgram!.UserID == userGuid)
            .Include(ts => ts.TrainingProgram)
            .Include(ts => ts.Movements)
            .ThenInclude(m => m.Sets)
            .Include(ts => ts.Movements)
            .ThenInclude(m => m.MovementBase)
            .Include(ts => ts.Movements)
            .ThenInclude(m => m.MovementModifier)
            .FirstOrDefaultAsync();

        if (session is null)
        {
            return Result<TrainingSessionDTO>.NotFound("Training session not found or access denied.");
        }

        // Calculate session number by counting sessions before this one in the same program
        var sessionNumber = await _context.TrainingSessions
            .Where(ts => ts.TrainingProgramID == session.TrainingProgramID &&
                        ts.Date <= session.Date)
            .OrderBy(ts => ts.Date)
            .ThenBy(ts => ts.TrainingSessionID) // For sessions on same date, use ID for consistent ordering
            .CountAsync(ts => ts.Date < session.Date ||
                            (ts.Date == session.Date && ts.TrainingSessionID.CompareTo(session.TrainingSessionID) <= 0));

        return Result<TrainingSessionDTO>.Success(session.ToDTO(sessionNumber));
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
            Date = date
        };

        _context.TrainingSessions.Add(session);
        await _context.SaveChangesAsync();

        // Calculate session number for the newly created session
        var sessionNumber = await _context.TrainingSessions
            .Where(ts => ts.TrainingProgramID == request.TrainingProgramID &&
                        ts.Date <= date)
            .OrderBy(ts => ts.Date)
            .ThenBy(ts => ts.TrainingSessionID)
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

        return Result<TrainingSessionDTO>.Success(session.ToDTO(sessionNumber));
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


    public async Task<Result<TrainingSessionDTO>> CreateTrainingSessionFromJSON(
            IdentityUser user,
            TrainingSessionDTO trainingSessionDTO)
        {
            // 1. Validate program exists
            var programId = trainingSessionDTO.TrainingProgramID;
            if (!await _context.TrainingPrograms.AnyAsync(p => p.TrainingProgramID == programId))
                return Result<TrainingSessionDTO>.Error("Invalid TrainingProgramID.");

        // 2) Create root session
            var newSession = new TrainingSession {
                TrainingSessionID = Guid.NewGuid(),
                TrainingProgramID = trainingSessionDTO.TrainingProgramID,
                Date              = trainingSessionDTO.Date,
                Status            = trainingSessionDTO.Status
            };

            int order = 0;
            foreach (var mDto in trainingSessionDTO.Movements)
            {
                // fetch the actual MovementBase entity so nav-prop is populated
                var baseEntity = await _context.MovementBases.FindAsync(mDto.MovementBaseID)!;

                var newMovement = new Movement {
                    MovementID        = Guid.NewGuid(),
                    TrainingSessionID = newSession.TrainingSessionID,
                    MovementBaseID    = mDto.MovementBaseID,
                    MovementBase      = baseEntity,            // ← set nav-prop
                    Notes             = mDto.Notes,
                    MovementModifier  = mDto.MovementModifier, // ← copy modifier
                    IsCompleted       = mDto.IsCompleted,      // ← copy completion
                    Ordering          = order++
                };

                foreach (var sDto in mDto.Sets)
                {
                    var newSet = new SetEntry {
                        SetEntryID       = Guid.NewGuid(),
                        MovementID       = newMovement.MovementID, // ← explicit FK
                        RecommendedReps  = sDto.RecommendedReps,
                        RecommendedWeight = sDto.RecommendedWeight,
                        RecommendedRPE   = sDto.RecommendedRPE,
                        WeightUnit       = sDto.WeightUnit,
                        ActualReps       = sDto.ActualReps,
                        ActualWeight     = sDto.ActualWeight,
                        ActualRPE        = sDto.ActualRPE
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
            .Include(ts => ts.Movements)!
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

    /// <summary>
    ///  Get the next training session for a user and program.
    ///  Returns the first planned session for the program.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="trainingProgramID"></param>
    /// <returns></returns>
    public async Task<Result<TrainingSessionDTO>> GetNextTrainingSessionAsync(IdentityUser user, Guid trainingProgramID)
    {
        var userGuid = Guid.Parse(user.Id);
        var nextSession = await _context.TrainingSessions
            .Where(ts => ts.TrainingProgramID == trainingProgramID &&
                        ts.TrainingProgram!.UserID == userGuid
                        && ts.Status == TrainingSessionStatus.Planned)
            .FirstOrDefaultAsync();

        if (nextSession is null)
        {
            return Result<TrainingSessionDTO>.NotFound("No upcoming training sessions found.");
        }

        var sessionNumber = await _context.TrainingSessions
            .Where(ts => ts.TrainingProgramID == nextSession.TrainingProgramID &&
                        ts.Date <= nextSession.Date)
            .OrderBy(ts => ts.Date)
            .ThenBy(ts => ts.TrainingSessionID)
            .CountAsync(ts => ts.Date < nextSession.Date ||
                            (ts.Date == nextSession.Date && ts.TrainingSessionID.CompareTo(nextSession.TrainingSessionID) <= 0));

        return Result<TrainingSessionDTO>.Success(nextSession.ToDTO(sessionNumber));
    }

    /// <summary>
    ///  Get the previous <paramref name="numberSessions"/>s for a user and program.
    ///  Returns a list of completed training sessions, ordered by date descending.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="trainingProgramID"></param>
    /// <param name="numberSessions"></param>
    /// <returns></returns>
    public async Task<Result<List<TrainingSessionDTO>>> GetPreviousTrainingSessionsAsync(IdentityUser user, Guid trainingProgramID, int numberSessions)
    {
        var userGuid = Guid.Parse(user.Id);
        var sessions = await _context.TrainingSessions
            .Where(ts => ts.TrainingProgramID == trainingProgramID &&
                        ts.TrainingProgram!.UserID == userGuid
                        && ts.Status == TrainingSessionStatus.Completed)
            .OrderByDescending(ts => ts.Date)
            .Take(numberSessions)
            .ToListAsync();

        if (sessions is null)
        {
            return Result<List<TrainingSessionDTO>>.NotFound("No training sessions found for this program.");
        }

        var sessionDTOs = sessions.Select((session, index) => session.ToDTO(index + 1)).ToList();
        return Result<List<TrainingSessionDTO>>.Success(sessionDTOs);
    }

   


}