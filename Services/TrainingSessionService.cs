using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services;

/// <summary>
/// Service for managing <see cref="TrainingSession"/>s within a training program.
/// </summary>
public class TrainingSessionService : ITrainingSessionService
{
    private readonly ModelContext _context;

    public TrainingSessionService(ModelContext context)
    {
        _context = context;
    }

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
}