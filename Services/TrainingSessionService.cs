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

    [McpServerTool, Description("Generate the next N sessions for a program with AI-selected movements, creating movement bases if needed")]
public async Task<Result<List<TrainingSessionDTO>>> GenerateTrainingSessionsAsync(
    IdentityUser user,
    GenerateTrainingSessionsRequest request)
{
    var userGuid = Guid.Parse(user.Id);

    // 1) Verify program ownership and fetch all sessions with movements
    var program = await _context.TrainingPrograms
        .Include(p => p.TrainingSessions)
            .ThenInclude(ts => ts.Movements)
        .FirstOrDefaultAsync(p =>
            p.TrainingProgramID == request.TrainingProgramID &&
            p.UserID           == userGuid);
    if (program is null)
        return Result<List<TrainingSessionDTO>>.NotFound("Program not found or access denied.");

    // 2) Find last date (or fallback to StartDate)
    var lastDate = program.TrainingSessions
        .Select(ts => ts.Date)
        .DefaultIfEmpty(program.StartDate)
        .Max();

    // 3) Define main lifts and assistance
    var mainLiftOrder = new[] { "Squat", "Bench Press", "Deadlift" };
    var assistanceName = "Barbell Row";
    var newSessions = new List<TrainingSession>();
    var newMovements = new List<Movement>();
    var newMovementBases = new List<MovementBase>();

    for (int i = 1; i <= request.Count; i++)
    {
        var sessionDate = lastDate.AddDays(i * 2); // 2 days apart for variety
        var session = new TrainingSession
        {
            TrainingSessionID = Guid.NewGuid(),
            TrainingProgramID = program.TrainingProgramID,
            Date              = sessionDate,
            Status            = TrainingSessionStatus.Planned,
            Movements         = new List<Movement>()
        };

        // Main lift
        var mainLiftName = mainLiftOrder[(program.TrainingSessions.Count + i - 1) % mainLiftOrder.Length];
        var mainLift = await _context.MovementBases.FirstOrDefaultAsync(mb => mb.Name.ToLower() == mainLiftName.ToLower());
        if (mainLift == null)
        {
            mainLift = new MovementBase { MovementBaseID = Guid.NewGuid(), Name = mainLiftName };
            _context.MovementBases.Add(mainLift);
            newMovementBases.Add(mainLift);
        }
        var mainMovement = new Movement
        {
            MovementID = Guid.NewGuid(),
            TrainingSessionID = session.TrainingSessionID,
            MovementBaseID = mainLift.MovementBaseID,
            MovementModifier = new MovementModifier { Name = "No Modifier" },
            Notes = $"Main lift: {mainLift.Name}",
            IsCompleted = false,
            Ordering = 1
        };
        session.Movements.Add(mainMovement);
        newMovements.Add(mainMovement);

        // Assistance movement
        var assistance = await _context.MovementBases.FirstOrDefaultAsync(mb => mb.Name.ToLower() == assistanceName.ToLower());
        if (assistance == null)
        {
            assistance = new MovementBase { MovementBaseID = Guid.NewGuid(), Name = assistanceName };
            _context.MovementBases.Add(assistance);
            newMovementBases.Add(assistance);
        }
        var assistMovement = new Movement
        {
            MovementID = Guid.NewGuid(),
            TrainingSessionID = session.TrainingSessionID,
            MovementBaseID = assistance.MovementBaseID,
            MovementModifier = new MovementModifier { Name = "No Modifier" },
            Notes = $"Assistance: {assistance.Name}",
            IsCompleted = false,
            Ordering = 2
        };
        session.Movements.Add(assistMovement);
        newMovements.Add(assistMovement);

        newSessions.Add(session);
    }
    _context.TrainingSessions.AddRange(newSessions);
    _context.Movements.AddRange(newMovements);
    if (newMovementBases.Count > 0)
        await _context.SaveChangesAsync(); // Save new movement bases before adding movements
    await _context.SaveChangesAsync();

    // 5) Map to DTOs (numbering after existing)
    var existingCount = program.TrainingSessions.Count;
    var dtos = newSessions
        .Select((s, idx) => s.ToDTO(existingCount + idx + 1))
        .ToList();

    return Result<List<TrainingSessionDTO>>.Success(dtos);
}

    public async Task<Result<TrainingSessionDTO>> CreateTrainingSessionFromJSON(IdentityUser user, TrainingSessionDTO trainingSessionDTO)
    {
        var userGuid = Guid.Parse(user.Id);
        var newSession = new TrainingSession
        {
            TrainingSessionID = System.Guid.NewGuid(),
            TrainingProgramID = trainingSessionDTO.TrainingProgramID,
            Date = trainingSessionDTO.Date,
            Status = trainingSessionDTO.Status
        };

        var movements = new List<Movement>();
        foreach (var movementDTO in trainingSessionDTO.Movements)
        {
            var movementBase = await _context.MovementBases.FindAsync(movementDTO.MovementBaseID);

            if (movementBase is null)
            {
                return Result<TrainingSessionDTO>.NotFound("Movement base not found.");
            }
            int count = 0;
            var newMovement = new Movement
            {
                MovementID = System.Guid.NewGuid(),
                MovementBaseID = movementDTO.MovementBaseID,
                MovementBase = movementBase,
                Notes = movementDTO.Notes,
                Ordering = count++,
                TrainingSessionID = newSession.TrainingSessionID,
                MovementModifier = movementDTO.MovementModifier ?? new MovementModifier(),
                IsCompleted = movementDTO.IsCompleted
            };

            var setEntries = new List<SetEntry>();
            foreach (var setEntryDTO in movementDTO.Sets)
            {
                var newSetEntry = new SetEntry
                {
                    SetEntryID = System.Guid.NewGuid(),
                    ActualReps = setEntryDTO.ActualReps,
                    ActualWeight = setEntryDTO.ActualWeight,
                    ActualRPE = setEntryDTO.ActualRPE,
                    RecommendedReps = setEntryDTO.RecommendedReps,
                    RecommendedWeight = setEntryDTO.RecommendedWeight,
                    RecommendedRPE = setEntryDTO.RecommendedRPE,
                    WeightUnit = setEntryDTO.WeightUnit
                };
                setEntries.Add(newSetEntry);
            }
            newMovement.Sets.AddRange(setEntries);
            movements.Add(newMovement);
        }
        newSession.Movements.AddRange(movements);
        await _context.TrainingSessions.AddAsync(newSession);
        await _context.Movements.AddRangeAsync(newSession.Movements);
        await _context.SetEntries.AddRangeAsync(newSession.Movements.SelectMany(m => m.Sets));
        await _context.SaveChangesAsync();
        return Result<TrainingSessionDTO>.Created(newSession.ToDTO(1));
    }

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
    public async Task<Result<TrainingSessionDTO>> GetLastTrainingSessionAsync(IdentityUser user, Guid trainingProgramID)
    {
        var userGuid = Guid.Parse(user.Id);
        var lastSession = await _context.TrainingSessions
            .Where(ts => ts.TrainingProgramID == trainingProgramID &&
                        ts.TrainingProgram!.UserID == userGuid 
                        && ts.Status == TrainingSessionStatus.Completed)
            .OrderByDescending(ts => ts.Date)
            .FirstOrDefaultAsync();

        if (lastSession is null)
        {
            return Result<TrainingSessionDTO>.NotFound("No training sessions found for this program.");
        }

        var sessionNumber = await _context.TrainingSessions
            .Where(ts => ts.TrainingProgramID == lastSession.TrainingProgramID &&
                        ts.Date <= lastSession.Date)
            .OrderBy(ts => ts.Date)
            .ThenBy(ts => ts.TrainingSessionID)
            .CountAsync(ts => ts.Date < lastSession.Date ||
                            (ts.Date == lastSession.Date && ts.TrainingSessionID.CompareTo(lastSession.TrainingSessionID) <= 0));

        return Result<TrainingSessionDTO>.Success(lastSession.ToDTO(sessionNumber));
    }
}