using Ardalis.ApiEndpoints;
using lionheart.Model.Training;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.PersonalRecord;

public class RevertPREndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<PersonalRecordDTO?>
{
    private readonly IPersonalRecordService _personalRecordService;
    private readonly UserManager<IdentityUser> _userManager;

    public RevertPREndpoint(IPersonalRecordService personalRecordService, UserManager<IdentityUser> userManager)
    {
        _personalRecordService = personalRecordService;
        _userManager = userManager;
    }

    [HttpPost("api/personal-records/revert/{personalRecordId:guid}")]
    [EndpointDescription("Revert a personal record to its previous state. Returns the previous PR if one exists, or null if there was no previous PR.")]
    // 200 with a null body is what "reverted, and there was no previous PR" looks like — the
    // route always answers with the DTO slot, so there is no 204 among its outcomes.
    [ProducesResponseType<PersonalRecordDTO>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<PersonalRecordDTO?>> HandleAsync([FromRoute] Guid personalRecordId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

        return this.ToActionResult(await _personalRecordService.RevertToPreviousAsync(user, personalRecordId));
    }
}
