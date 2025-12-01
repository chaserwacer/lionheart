using System.ComponentModel;
using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using Mapster;

namespace lionheart.Services;

public interface ITrainingProgramService
{
    /// <summary>
    /// Get all programs for a user.
    /// </summary>
    /// <param name="user">The user whose programs to retrieve.</param>
    /// <returns>A result containing a list of programs.</returns>
    Task<Result<List<TrainingProgramDTO>>> GetTrainingProgramsAsync(IdentityUser user);

    /// <summary>
    /// Get a specific program by ID for a user.
    /// </summary>
    /// <param name="user">The user who owns the program.</param>
    /// <param name="programId">The program ID to retrieve.</param>
    /// <returns>A result containing the program.</returns>
    Task<Result<TrainingProgramDTO>> GetTrainingProgramAsync(IdentityUser user, Guid programId);

    /// <summary>
    /// Create a new training program for a user.
    /// </summary>
    /// <param name="user">The user to create the program for.</param>
    /// <param name="request">The program creation request.</param>
    /// <returns>A result containing the created program.</returns>
    Task<Result<TrainingProgramDTO>> CreateTrainingProgramAsync(IdentityUser user, CreateTrainingProgramRequest request);

    /// <summary>
    /// Update an existing training program.
    /// </summary>
    /// <param name="user">The user who owns the program.</param>
    /// <param name="request">The program update request.</param>
    /// <returns>A result containing the updated program.</returns>
    Task<Result<TrainingProgramDTO>> UpdateTrainingProgramAsync(IdentityUser user, UpdateTrainingProgramRequest request);

    /// <summary>
    /// Delete a training program.
    /// </summary>
    /// <param name="user">The user who owns the program.</param>
    /// <param name="programId">The program ID to delete.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> DeleteTrainingProgramAsync(IdentityUser user, Guid programId);

    
}

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
            .OrderBy(p => p.StartDate)
            .ProjectToType<TrainingProgramDTO>()
            .ToListAsync();

        return Result<List<TrainingProgramDTO>>.Success(trainingPrograms);
    }

    [McpServerTool, Description("Get a specific program by ID for the current user.")]
    public async Task<Result<TrainingProgramDTO>> GetTrainingProgramAsync(IdentityUser user, Guid TrainingprogramId)
    {
        var userGuid = Guid.Parse(user.Id);
        var trainingProgramDto = await _context.TrainingPrograms
            .Where(p => p.TrainingProgramID == TrainingprogramId && p.UserID == userGuid)
            .ProjectToType<TrainingProgramDTO>()
            .FirstOrDefaultAsync();

        if (trainingProgramDto is null)
        {
            return Result<TrainingProgramDTO>.NotFound("TrainingProgram not found.");
        }

        return Result<TrainingProgramDTO>.Success(trainingProgramDto);
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
            TrainingSessions = [],
            UserID = userGuid,
            Title = request.Title,
            StartDate = startDate,
            EndDate = endDate,
            Tags = request.Tags ?? [],
            IsCompleted = false,
        };

        _context.TrainingPrograms.Add(trainingProgram);
        await _context.SaveChangesAsync();
        return Result<TrainingProgramDTO>.Created(trainingProgram.Adapt<TrainingProgramDTO>());
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
        trainingProgram.IsCompleted = request.IsCompleted;
        trainingProgram.Tags = request.Tags;

        await _context.SaveChangesAsync();
        return Result<TrainingProgramDTO>.Success(trainingProgram.Adapt<TrainingProgramDTO>());
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

   
}