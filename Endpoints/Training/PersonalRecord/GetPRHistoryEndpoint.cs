using Ardalis.ApiEndpoints;
using Ardalis.Result.AspNetCore;
using lionheart.Model.Training;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.PersonalRecord;

public class GetPRHistoryRequest
{
    [FromRoute]
    public Guid MovementDataId { get; set; }

    [FromQuery]
    public PersonalRecordType PRType { get; set; }
}

public class GetPRHistoryEndpoint : EndpointBaseAsync
    .WithRequest<GetPRHistoryRequest>
    .WithActionResult<List<PersonalRecordDTO>>
{
    private readonly IPersonalRecordService _personalRecordService;
    private readonly UserManager<IdentityUser> _userManager;

    public GetPRHistoryEndpoint(IPersonalRecordService personalRecordService, UserManager<IdentityUser> userManager)
    {
        _personalRecordService = personalRecordService;
        _userManager = userManager;
    }

    [HttpGet("api/personal-records/history/{movementDataId:guid}")]
    [EndpointDescription("Get the history of PRs for a specific movement data and PR type.")]
    [ProducesResponseType<List<PersonalRecordDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<List<PersonalRecordDTO>>> HandleAsync([FromRoute] GetPRHistoryRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

        return this.ToActionResult(await _personalRecordService.GetPRHistoryAsync(user, request.MovementDataId, request.PRType));
    }
}
