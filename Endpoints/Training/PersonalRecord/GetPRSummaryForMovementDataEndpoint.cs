using Ardalis.ApiEndpoints;
using Ardalis.Result.AspNetCore;
using lionheart.Model.Training;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.PersonalRecord;

public class GetPRSummaryForMovementDataEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<MovementDataPRSummary>
{
    private readonly IPersonalRecordService _personalRecordService;
    private readonly UserManager<IdentityUser> _userManager;

    public GetPRSummaryForMovementDataEndpoint(IPersonalRecordService personalRecordService, UserManager<IdentityUser> userManager)
    {
        _personalRecordService = personalRecordService;
        _userManager = userManager;
    }

    [HttpGet("api/personal-records/summary/{movementDataId:guid}")]
    [EndpointDescription("Get PR summary for a specific movement data.")]
    [ProducesResponseType<MovementDataPRSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<MovementDataPRSummary>> HandleAsync([FromRoute] Guid movementDataId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

        return this.ToActionResult(await _personalRecordService.GetPRSummaryForMovementDataAsync(user, movementDataId));
    }
}
