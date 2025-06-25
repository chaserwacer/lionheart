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
        public List<AiMovement> Movements { get; set; } = new();
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

    public TrainingSessionService(ModelContext context, IMCPClientService aiClient,  ILogger<MCPClientService> logger)
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

    [McpServerTool, Description("Generate additional training sessions via AI.")]
    public async Task<Result<List<TrainingSessionDTO>>> GenerateTrainingSessionsAsync(IdentityUser user, GenerateTrainingSessionsRequest request)
    {
        var userGuid = Guid.Parse(user.Id);

        // 1) Load program + its last sessions (with movements & sets)
        var program = await _context.TrainingPrograms
            .Include(p => p.TrainingSessions)
                .ThenInclude(ts => ts.Movements)
                    .ThenInclude(m => m.Sets)
            .FirstOrDefaultAsync(p =>
                p.TrainingProgramID == request.TrainingProgramID &&
                p.UserID           == userGuid);
        if (program is null)
            return Result<List<TrainingSessionDTO>>.NotFound();

        // 2) Prepare the list of existing session DTOs
        var existingDtos = program.TrainingSessions
            .OrderBy(ts => ts.Date)
            .Select((s, i) => s.ToDTO(i + 1))
            .ToList();

        // 3) Build the prompt and invoke your helper
        var prompt = new LionMcpPrompt { User = user };
        prompt.AddGenerateTrainingSessionsSection(
            program.Tags,
            existingDtos,
            request.Count);
        
        var messages   = prompt.ToChatMessage();
        var chatResult = await _ai.ChatAsync(user, messages);
        if (!chatResult.IsSuccess)
            return Result<List<TrainingSessionDTO>>.Error("AI generation failed.");

        // 4) Pull out the JSON array from the AI’s reply
        var raw = chatResult.Value!.Trim();
        var idx = raw.IndexOf('[');
        if (idx < 0)
            return Result<List<TrainingSessionDTO>>.Error("No JSON found in AI response.");
        var json = raw[idx..];

        List<AiSession>? aiSessions;
        try
        {
            _logger?.LogInformation("RAW AI output:\n{Raw}", chatResult.Value);

            aiSessions = JsonSerializer.Deserialize<List<AiSession>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException je)
        {
            return Result<List<TrainingSessionDTO>>.Error($"JSON parse error: {je.Message}");
        }
        if (aiSessions is null)
            return Result<List<TrainingSessionDTO>>.Error("AI returned no sessions.");

        // 5) Persist the new sessions, movements, and set‐entries
        var createdDtos = new List<TrainingSessionDTO>();
        foreach (var ai in aiSessions)
        {
            var session = new TrainingSession
            {
                TrainingSessionID = Guid.NewGuid(),
                TrainingProgramID = program.TrainingProgramID,
                Date              = ai.Date,
                Status            = TrainingSessionStatus.Planned
            };
            _context.TrainingSessions.Add(session);

            int order = 1;
            foreach (var m in ai.Movements)
            {
                var mv = new Movement
                {
                    MovementID        = Guid.NewGuid(),
                    TrainingSessionID = session.TrainingSessionID,
                    MovementBaseID    = m.MovementBaseID,
                    MovementModifier  = m.Modifier,
                    Notes             = m.Notes,
                    IsCompleted       = false,
                    Ordering          = order++
                };
                _context.Movements.Add(mv);

                _context.SetEntries.Add(new SetEntry
                {
                    SetEntryID        = Guid.NewGuid(),
                    MovementID        = mv.MovementID,
                    RecommendedReps   = m.Reps,
                    RecommendedWeight = m.Weight,
                    RecommendedRPE    = m.RPE,
                    WeightUnit        = m.Unit
                });
            }

            await _context.SaveChangesAsync();

            var number = await _context.TrainingSessions
                .CountAsync(ts =>
                    ts.TrainingProgramID == program.TrainingProgramID &&
                    ts.Date           <= session.Date);
            createdDtos.Add(session.ToDTO(number));
        }

        return Result<List<TrainingSessionDTO>>.Success(createdDtos);
    }
}