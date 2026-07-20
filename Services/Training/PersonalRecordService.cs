using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Model.Chat.Tools;

namespace lionheart.Services;

public interface IPersonalRecordService
{
    /// <summary>
    /// Gets all active personal records for a user.
    /// </summary>
    Task<Result<List<PersonalRecordDTO>>> GetPersonalRecordsAsync(IdentityUser user);

    /// <summary>
    /// Gets the PR summary for a specific MovementData.
    /// </summary>
    Task<Result<MovementDataPRSummary>> GetPRSummaryForMovementDataAsync(IdentityUser user, Guid movementDataId);

    /// <summary>
    /// Gets all PR summaries for all MovementDatas the user has PRs for.
    /// </summary>
    Task<Result<List<MovementDataPRSummary>>> GetAllPRSummariesAsync(IdentityUser user);

    /// <summary>
    /// Reverts a PR to its previous state.
    /// </summary>
    Task<Result<PersonalRecordDTO?>> RevertToPreviousAsync(IdentityUser user, Guid personalRecordId);

    /// <summary>
    /// Gets the history of PRs for a specific MovementData and type.
    /// </summary>
    Task<Result<List<PersonalRecordDTO>>> GetPRHistoryAsync(IdentityUser user, Guid movementDataId, PersonalRecordType prType);
}
[ToolProvider]
public class PersonalRecordService : IPersonalRecordService
{
    private readonly ModelContext _context;

    public PersonalRecordService(ModelContext context)
    {
        _context = context;
    }
    [Tool(Name = "GetPersonalRecords", Description = "Get all active personal records for the user.")]
    public async Task<Result<List<PersonalRecordDTO>>> GetPersonalRecordsAsync(IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);
        var records = await _context.PersonalRecords
            .Where(pr => pr.UserID == userGuid && pr.IsActive)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.Equipment)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.MovementBase)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.MovementModifier)
            .OrderByDescending(pr => pr.CreatedAt)
            .ToListAsync();

        return Result<List<PersonalRecordDTO>>.Success(records.Select(r => r.ToDTO()).ToList());
    }
    [Tool(Name = "GetPRSummaryForMovementData", Description = "Get PR summary for a specific MovementData.")]
    public async Task<Result<MovementDataPRSummary>> GetPRSummaryForMovementDataAsync(IdentityUser user, Guid movementDataId)
    {
        var userGuid = Guid.Parse(user.Id);

        var movementData = await _context.MovementDatas
            .Include(md => md.Equipment)
            .Include(md => md.MovementBase)
            .Include(md => md.MovementModifier)
            .FirstOrDefaultAsync(md => md.MovementDataID == movementDataId && md.UserID == userGuid);

        if (movementData == null)
        {
            return Result<MovementDataPRSummary>.NotFound("MovementData not found.");
        }

        var strengthPR = await _context.PersonalRecords
            .Where(pr => pr.UserID == userGuid && pr.MovementDataID == movementDataId && pr.PRType == PersonalRecordType.Strength && pr.IsActive)
            .Include(pr => pr.MovementData).ThenInclude(md => md.Equipment)
            .Include(pr => pr.MovementData).ThenInclude(md => md.MovementBase)
            .Include(pr => pr.MovementData).ThenInclude(md => md.MovementModifier)
            .FirstOrDefaultAsync();

        var volumePR = await _context.PersonalRecords
            .Where(pr => pr.UserID == userGuid && pr.MovementDataID == movementDataId && pr.PRType == PersonalRecordType.Volume && pr.IsActive)
            .Include(pr => pr.MovementData).ThenInclude(md => md.Equipment)
            .Include(pr => pr.MovementData).ThenInclude(md => md.MovementBase)
            .Include(pr => pr.MovementData).ThenInclude(md => md.MovementModifier)
            .FirstOrDefaultAsync();

        var lastPRDate = new[] { strengthPR?.CreatedAt, volumePR?.CreatedAt }
            .Where(d => d.HasValue)
            .Max();

        return Result<MovementDataPRSummary>.Success(new MovementDataPRSummary(
            movementDataId,
            movementData.ToDTO(),
            strengthPR?.ToDTO(),
            volumePR?.ToDTO(),
            lastPRDate
        ));
    }
    public async Task<Result<List<MovementDataPRSummary>>> GetAllPRSummariesAsync(IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);

        // Get all MovementDatas that have active PRs
        var movementDataIds = await _context.PersonalRecords
            .Where(pr => pr.UserID == userGuid && pr.IsActive)
            .Select(pr => pr.MovementDataID)
            .Distinct()
            .ToListAsync();

        var summaries = new List<MovementDataPRSummary>();
        foreach (var mdId in movementDataIds)
        {
            var result = await GetPRSummaryForMovementDataAsync(user, mdId);
            if (result.IsSuccess)
            {
                summaries.Add(result.Value);
            }
        }

        return Result<List<MovementDataPRSummary>>.Success(summaries.OrderByDescending(s => s.LastPRDate).ToList());
    }

    public async Task<Result<PersonalRecordDTO?>> RevertToPreviousAsync(IdentityUser user, Guid personalRecordId)
    {
        var userGuid = Guid.Parse(user.Id);

        var pr = await _context.PersonalRecords
            .Include(pr => pr.PreviousPersonalRecord)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.Equipment)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.MovementBase)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.MovementModifier)
            .FirstOrDefaultAsync(pr => pr.PersonalRecordID == personalRecordId && pr.UserID == userGuid);

        if (pr == null)
        {
            return Result<PersonalRecordDTO?>.NotFound("Personal record not found.");
        }

        if (!pr.IsActive)
        {
            return Result<PersonalRecordDTO?>.Error("Cannot revert an inactive personal record.");
        }

        // Deactivate current PR
        pr.IsActive = false;

        // Reactivate previous PR if it exists
        if (pr.PreviousPersonalRecord != null)
        {
            pr.PreviousPersonalRecord.IsActive = true;
            await _context.SaveChangesAsync();
            return Result<PersonalRecordDTO?>.Success(pr.PreviousPersonalRecord.ToDTO());
        }

        await _context.SaveChangesAsync();
        return Result<PersonalRecordDTO?>.Success(null);
    }

    public async Task<Result<List<PersonalRecordDTO>>> GetPRHistoryAsync(IdentityUser user, Guid movementDataId, PersonalRecordType prType)
    {
        var userGuid = Guid.Parse(user.Id);

        var history = await _context.PersonalRecords
            .Where(pr => pr.UserID == userGuid && pr.MovementDataID == movementDataId && pr.PRType == prType)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.Equipment)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.MovementBase)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.MovementModifier)
            .OrderByDescending(pr => pr.CreatedAt)
            .ToListAsync();

        return Result<List<PersonalRecordDTO>>.Success(history.Select(h => h.ToDTO()).ToList());
    }

}
