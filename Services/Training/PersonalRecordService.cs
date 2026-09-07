using lionheart.Functional;
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
    Task<Result<List<PersonalRecordDTO>, Problem>> GetPersonalRecordsAsync(IdentityUser user);

    /// <summary>
    /// Gets the PR summary for a specific MovementData.
    /// </summary>
    Task<Result<MovementDataPRSummary, Problem>> GetPRSummaryForMovementDataAsync(IdentityUser user, Guid movementDataId);

    /// <summary>
    /// Gets all PR summaries for all MovementDatas the user has PRs for.
    /// </summary>
    Task<Result<List<MovementDataPRSummary>, Problem>> GetAllPRSummariesAsync(IdentityUser user);

    /// <summary>
    /// Reverts a PR to its previous state.
    /// </summary>
    Task<Result<PersonalRecordDTO?, Problem>> RevertToPreviousAsync(IdentityUser user, Guid personalRecordId);

    /// <summary>
    /// Gets the history of PRs for a specific MovementData and type.
    /// </summary>
    Task<Result<List<PersonalRecordDTO>, Problem>> GetPRHistoryAsync(IdentityUser user, Guid movementDataId, PersonalRecordType prType);
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
    public async Task<Result<List<PersonalRecordDTO>, Problem>> GetPersonalRecordsAsync(IdentityUser user)
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

        return Result<List<PersonalRecordDTO>, Problem>.Success(records.Select(r => r.ToDTO()).ToList());
    }
    [Tool(Name = "GetPRSummaryForMovementData", Description = "Get PR summary for a specific MovementData.")]
    public async Task<Result<MovementDataPRSummary, Problem>> GetPRSummaryForMovementDataAsync(IdentityUser user, Guid movementDataId)
    {
        var userGuid = Guid.Parse(user.Id);

        var movementData = await _context.MovementDatas
            .Include(md => md.Equipment)
            .Include(md => md.MovementBase)
            .Include(md => md.MovementModifier)
            .FirstOrDefaultAsync(md => md.MovementDataID == movementDataId && md.UserID == userGuid);

        if (movementData == null)
        {
            return Result<MovementDataPRSummary, Problem>.Failure(new Problem.NotFound("MovementData not found."));
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

        return Result<MovementDataPRSummary, Problem>.Success(new MovementDataPRSummary(
            movementDataId,
            movementData.ToDTO(),
            strengthPR?.ToDTO(),
            volumePR?.ToDTO(),
            lastPRDate
        ));
    }
    public async Task<Result<List<MovementDataPRSummary>, Problem>> GetAllPRSummariesAsync(IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);

        // Get all MovementDatas that have active PRs
        var movementDataIds = await _context.PersonalRecords
            .Where(pr => pr.UserID == userGuid && pr.IsActive)
            .Select(pr => pr.MovementDataID)
            .Distinct()
            .ToListAsync();

        var summaries = new List<MovementDataPRSummary>();
        foreach (var movementDataId in movementDataIds)
        {
            // A movement whose summary cannot be built is simply absent from the list rather
            // than failing the whole page.
            if (await GetPRSummaryForMovementDataAsync(user, movementDataId)
                is Success<MovementDataPRSummary, Problem> summary)
            {
                summaries.Add(summary.Value);
            }
        }

        return Result<List<MovementDataPRSummary>, Problem>.Success(summaries.OrderByDescending(s => s.LastPRDate).ToList());
    }

    public Task<Result<PersonalRecordDTO?, Problem>> RevertToPreviousAsync(IdentityUser user, Guid personalRecordId) =>
        FindRevertableAsync(Guid.Parse(user.Id), personalRecordId)
            // A record that has already been reverted collides with the state it is in, rather
            // than being a malformed ask: the same request was legitimate a moment earlier.
            .EnsureAsync(
                pr => pr.IsActive,
                _ => new Problem.Conflict("Cannot revert an inactive personal record."))
            .MapAsync(RevertAsync);

    private async Task<Result<PersonalRecord, Problem>> FindRevertableAsync(Guid userId, Guid personalRecordId)
    {
        var pr = await _context.PersonalRecords
            .Include(pr => pr.PreviousPersonalRecord)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.Equipment)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.MovementBase)
            .Include(pr => pr.MovementData)
                .ThenInclude(md => md.MovementModifier)
            .FirstOrDefaultAsync(pr => pr.PersonalRecordID == personalRecordId && pr.UserID == userId);

        return pr is null
            ? Result<PersonalRecord, Problem>.Failure(new Problem.NotFound("Personal record not found."))
            : Result<PersonalRecord, Problem>.Success(pr);
    }

    /// <summary>
    /// Steps the PR back one link in its own chain. A record that was the first of its kind has
    /// nothing behind it to restore, which is why nothing comes back in that case.
    /// </summary>
    private async Task<PersonalRecordDTO?> RevertAsync(PersonalRecord pr)
    {
        pr.IsActive = false;
        if (pr.PreviousPersonalRecord is not null) pr.PreviousPersonalRecord.IsActive = true;

        await _context.SaveChangesAsync();
        return pr.PreviousPersonalRecord?.ToDTO();
    }

    public async Task<Result<List<PersonalRecordDTO>, Problem>> GetPRHistoryAsync(IdentityUser user, Guid movementDataId, PersonalRecordType prType)
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

        return Result<List<PersonalRecordDTO>, Problem>.Success(history.Select(h => h.ToDTO()).ToList());
    }

}
