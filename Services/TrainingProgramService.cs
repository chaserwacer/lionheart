using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services;

/// <summary>
/// Service for managing <see cref="TrainingProgram"/>s.
/// </summary>
public class TrainingProgramService : ITrainingProgramService
{
    private readonly ModelContext _context;

    public TrainingProgramService(ModelContext context)
    {
        _context = context;
    }

    public async Task<Result<List<TrainingProgramDTO>>> GetTrainingProgramsAsync(IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);
        var trainingPrograms = await _context.TrainingPrograms
            .Where(p => p.UserID == userGuid)
            .Include(p => p.TrainingSessions)
            .ThenInclude(ts => ts.Movements)
            .ThenInclude(m => m.Sets)
            .OrderBy(p => p.StartDate)
            .ToListAsync();

        return Result<List<TrainingProgramDTO>>.Success(trainingPrograms.Select(p => p.ToDTO()).ToList());
    }

    public async Task<Result<TrainingProgramDTO>> GetTrainingProgramAsync(IdentityUser user, Guid TrainingprogramId)
    {
        var userGuid = Guid.Parse(user.Id);
        var trainingProgram = await _context.TrainingPrograms
            .Where(p => p.TrainingProgramID == TrainingprogramId && p.UserID == userGuid)
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