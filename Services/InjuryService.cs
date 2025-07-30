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
    public async Task<Result<InjuryDTO>> AddInjuryEventAsync(IdentityUser user, Guid injuryId, CreateInjuryEventRequest request)
    {
        var userId = Guid.Parse(user.Id);
        var injury = await _context.Injuries
            .Include(i => i.InjuryEvents)
            .FirstOrDefaultAsync(i => i.InjuryID == injuryId && i.UserID == userId);

        if (injury is null)
            return Result<InjuryDTO>.NotFound("Injury not found");

        var newEvent = new InjuryEvent
        {
            TrainingSessionID = request.TrainingSessionID,
            Notes = request.Notes,
            PainLevel = request.PainLevel,
            InjuryType = request.InjuryType,
            CreationTime = DateTime.UtcNow
        };

        injury.InjuryEvents.Add(newEvent);
        await _context.SaveChangesAsync();

        return Result<InjuryDTO>.Success(injury.ToDTO());
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

    
}
