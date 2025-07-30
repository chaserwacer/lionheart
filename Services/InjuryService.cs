using System.ComponentModel;
using Ardalis.Result;
using lionheart.Model.DTOs;
using lionheart.Model.Injury;
using lionheart.Model.Mappers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using lionheart.Data;


namespace lionheart.Services;

public class InjuryService : IInjuryService
{
    private readonly ModelContext _context;

    public InjuryService(ModelContext context)
    {
        _context = context;
    }


    public async Task<Result<InjuryDTO>> CreateInjuryAsync(IdentityUser user, CreateInjuryRequest request)
    {
        var userId = Guid.Parse(user.Id);
        var injury = new Injury
        {
            InjuryID = Guid.NewGuid(),
            UserID = userId,
            Category = request.Category,
            InjuryDate = request.InjuryDate,
            IsResolved = false,
            InjuryEvents = new()
        };

        _context.Injuries.Add(injury);
        await _context.SaveChangesAsync();

        return Result<InjuryDTO>.Created(injury.ToDTO());
    }
    public async Task<Result<InjuryDTO>> AddInjuryEventAsync(
        IdentityUser user,
        Guid injuryId,
        CreateInjuryEventRequest request)
    {
        var userId = Guid.Parse(user.Id);

        // 1) Verify the injury exists and belongs to the user
        var injury = await _context.Injuries
        .FirstOrDefaultAsync(i => i.InjuryID == injuryId && i.UserID == userId);
        if (injury is null)
            return Result<InjuryDTO>.NotFound("Injury not found");

        // 2) (Optional) Verify the session exists
        var session = await _context.TrainingSessions
        .FindAsync(request.TrainingSessionID);
        if (session is null)
            return Result<InjuryDTO>.Error("Training session not found");

        // 3) Create the event, explicitly set the FK
        var newEvent = new InjuryEvent
        {
            InjuryID = injuryId,
            TrainingSessionID = request.TrainingSessionID,
            Notes = request.Notes,
            PainLevel = request.PainLevel,
            InjuryType = request.InjuryType,
            CreationTime = DateTime.UtcNow
        };

        // 4) Add the event directly to its DbSet
        await _context.InjuryEvents.AddAsync(newEvent);
        await _context.SaveChangesAsync();

        // 5) Re-load the injury (including its events) so we return up-to-date data
        var updatedInjury = await _context.Injuries
            .Where(i => i.InjuryID == injuryId)
            .Include(i => i.InjuryEvents)
            .FirstAsync();

        return Result<InjuryDTO>.Success(updatedInjury.ToDTO());
    }

    public async Task<Result<List<InjuryDTO>>> GetUserInjuriesAsync(IdentityUser user)
    {
        var userId = Guid.Parse(user.Id);
        var injuries = await _context.Injuries
            .Where(i => i.UserID == userId)
            .Include(i => i.InjuryEvents)
            .ToListAsync();

        return Result<List<InjuryDTO>>.Success(injuries.Select(i => i.ToDTO()).ToList());
    }

    [McpServerTool, Description("Mark an injury as resolved.")]
    public async Task<Result> MarkInjuryResolvedAsync(IdentityUser user, Guid injuryId)
    {
        var userId = Guid.Parse(user.Id);
        var injury = await _context.Injuries
            .FirstOrDefaultAsync(i => i.InjuryID == injuryId && i.UserID == userId);

        if (injury is null)
            return Result.NotFound("Injury not found");

        injury.IsResolved = true;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> DeleteInjuryAsync(IdentityUser user, Guid injuryId)
    {
        var userId = Guid.Parse(user.Id);

        var injury = await _context.Injuries
            .Include(i => i.InjuryEvents)
            .FirstOrDefaultAsync(i => i.InjuryID == injuryId && i.UserID == userId);

        if (injury is null)
            return Result.NotFound("Injury not found");

        _context.InjuryEvents.RemoveRange(injury.InjuryEvents);
        _context.Injuries.Remove(injury);
        await _context.SaveChangesAsync();

        return Result.Success();
    }
}