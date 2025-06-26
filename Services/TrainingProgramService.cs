using System.ComponentModel;
using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace lionheart.Services;

/// <summary>
/// Service for managing <see cref="TrainingProgram"/>s.
/// </summary>
[McpServerToolType]
public class TrainingProgramService : ITrainingProgramService
{
    private readonly ModelContext _context;

    public TrainingProgramService(ModelContext context)
    {
        _context = context;
    }

    [McpServerTool, Description("Get all programs for the current user.")]
    public async Task<Result<List<TrainingProgramDTO>>> GetTrainingProgramsAsync(IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);
        var trainingPrograms = await _context.TrainingPrograms
            .Where(p => p.UserID == userGuid)
            .Include(p => p.TrainingSessions)
            .ThenInclude(ts => ts.Movements)
                .ThenInclude(m => m.MovementBase)
            .Include(p => p.TrainingSessions)
            .ThenInclude(ts => ts.Movements)
                .ThenInclude(m => m.Sets)
            .OrderBy(p => p.StartDate)
            .ToListAsync();

        return Result<List<TrainingProgramDTO>>.Success(trainingPrograms.Select(p => p.ToDTO()).ToList());
    }

    [McpServerTool, Description("Get a specific program by ID for the current user.")]
    public async Task<Result<TrainingProgramDTO>> GetTrainingProgramAsync(IdentityUser user, Guid TrainingprogramId)
    {
        var userGuid = Guid.Parse(user.Id);
        var trainingProgram = await _context.TrainingPrograms
            .Where(p => p.TrainingProgramID == TrainingprogramId && p.UserID == userGuid)
            .Include(p => p.TrainingSessions)
            .ThenInclude(ts => ts.Movements)
                .ThenInclude(m => m.MovementBase)
            .Include(p => p.TrainingSessions)
            .ThenInclude(ts => ts.Movements)
                .ThenInclude(m => m.Sets)
            .FirstOrDefaultAsync();

        if (trainingProgram is null)
        {
            return Result<TrainingProgramDTO>.NotFound("TrainingProgram not found.");
        }

        return Result<TrainingProgramDTO>.Success(trainingProgram.ToDTO());
    }

    [McpServerTool, Description("Create a new training program.")]
    public async Task<Result<TrainingProgramDTO>> CreateTrainingProgramAsync(IdentityUser user, CreateTrainingProgramRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var startDate = request.StartDate;
        var endDate = request.EndDate;


        var trainingProgram = new TrainingProgram
        {
            TrainingProgramID = Guid.NewGuid(),
            UserID = userGuid,
            Title = request.Title,
            StartDate = startDate,
            EndDate = endDate,
            Tags = request.Tags ?? []
        };

        _context.TrainingPrograms.Add(trainingProgram);
        await _context.SaveChangesAsync();
        return Result<TrainingProgramDTO>.Created(trainingProgram.ToDTO());
    }

    [McpServerTool, Description("Update an existing training program.")]
    public async Task<Result<TrainingProgramDTO>> UpdateTrainingProgramAsync(IdentityUser user, UpdateTrainingProgramRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var trainingProgram = await _context.TrainingPrograms
            .FirstOrDefaultAsync(p => p.TrainingProgramID == request.TrainingProgramID && p.UserID == userGuid);

        if (trainingProgram is null)
        {
            return Result<TrainingProgramDTO>.NotFound("TrainingProgram not found.");
        }

        trainingProgram.Title = request.Title;

        trainingProgram.StartDate = request.StartDate;


        trainingProgram.EndDate = request.EndDate;


        trainingProgram.Tags = request.Tags;


        await _context.SaveChangesAsync();
        return Result<TrainingProgramDTO>.Success(trainingProgram.ToDTO());
    }

    [McpServerTool, Description("Delete a training program.")]
    public async Task<Result> DeleteTrainingProgramAsync(IdentityUser user, Guid TrainingprogramId)
    {
        var userGuid = Guid.Parse(user.Id);
        var trainingProgram = await _context.TrainingPrograms
            .FirstOrDefaultAsync(p => p.TrainingProgramID == TrainingprogramId && p.UserID == userGuid);

        if (trainingProgram is null)
        {
            return Result.NotFound("TrainingProgram not found.");
        }

        _context.TrainingPrograms.Remove(trainingProgram);
        await _context.SaveChangesAsync();
        return Result.NoContent();
    }
    
    [McpServerTool, Description("Generate a populated training program with sessions and movements.")]
    public async Task<Result<TrainingProgramDTO>> GeneratePopulatedTrainingProgramAsync(IdentityUser user, GeneratePopulatedTrainingProgramRequest request)
    {
        var userGuid = Guid.Parse(user.Id);
        var trainingProgram = new TrainingProgram
        {
            TrainingProgramID = Guid.NewGuid(),
            UserID = userGuid,
            Title = request.TrainingProgram.Title,
            StartDate = request.TrainingProgram.StartDate,
            EndDate = request.TrainingProgram.EndDate,
            Tags = request.TrainingProgram.Tags ?? []
        };

        _context.TrainingPrograms.Add(trainingProgram);
        await _context.SaveChangesAsync();

        foreach (var sessionRequest in request.GenerateTrainingSessionsRequests)
        {
            var trainingSession = new TrainingSession
            {
                TrainingSessionID = Guid.NewGuid(),
                TrainingProgramID = trainingProgram.TrainingProgramID,
                Date = sessionRequest.createTrainingSessionRequest.Date,
                Status = TrainingSessionStatus.Planned         
            };

            _context.TrainingSessions.Add(trainingSession);
            await _context.SaveChangesAsync();

            int ordering = 0;
            foreach (var movementRequest in sessionRequest.CreateMovementRequests)
            {
                var movementBase = await _context.MovementBases
                    .FindAsync(movementRequest.MovementBaseID);

                if (movementBase is null)
                {
                    return Result<TrainingProgramDTO>.Error($"Movement Base with ID:'{movementRequest.MovementBaseID}' not found.");
                }

                var movement = new Movement
                {
                    MovementID = Guid.NewGuid(),
                    TrainingSessionID = trainingSession.TrainingSessionID,
                    MovementBaseID = movementBase.MovementBaseID,
                    Notes = movementRequest.Notes,
                    Ordering = ordering
                };

                _context.Movements.Add(movement);
                await _context.SaveChangesAsync();

                foreach (var setEntryRequest in sessionRequest.CreateSetEntryRequests)
                {
                    var setEntry = new SetEntry
                    {
                        SetEntryID = Guid.NewGuid(),
                        MovementID = movement.MovementID,
                        ActualReps = setEntryRequest.ActualReps,
                        ActualWeight = setEntryRequest.ActualWeight,
                        ActualRPE = setEntryRequest.ActualRPE,
                        RecommendedReps = setEntryRequest.RecommendedReps,
                        RecommendedWeight = setEntryRequest.RecommendedWeight,
                        RecommendedRPE = setEntryRequest.RecommendedRPE,
                        WeightUnit = setEntryRequest.WeightUnit
                    };

                    _context.SetEntries.Add(setEntry);
                }

                ordering++;
            }
        }

        await _context.SaveChangesAsync();
        return Result<TrainingProgramDTO>.Success(trainingProgram.ToDTO());
    }


}