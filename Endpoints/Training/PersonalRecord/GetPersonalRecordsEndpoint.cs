using Ardalis.ApiEndpoints;
using Ardalis.Result.AspNetCore;
using lionheart.Model.Training;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.PersonalRecord;

public class GetPersonalRecordsEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<List<PersonalRecordDTO>>
{
    private readonly IPersonalRecordService _personalRecordService;
    private readonly UserManager<IdentityUser> _userManager;

    public GetPersonalRecordsEndpoint(IPersonalRecordService personalRecordService, UserManager<IdentityUser> userManager)
    {
        _personalRecordService = personalRecordService;
        _userManager = userManager;
    }

    [HttpGet("api/personal-records")]
    [EndpointDescription("Get all active personal records for the current user.")]
    [ProducesResponseType<List<PersonalRecordDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<List<PersonalRecordDTO>>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

        return this.ToActionResult(await _personalRecordService.GetPersonalRecordsAsync(user));
    }
}
