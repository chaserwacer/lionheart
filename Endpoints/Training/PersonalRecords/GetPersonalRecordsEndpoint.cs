using Ardalis.ApiEndpoints;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.PersonalRecords;

/// <summary>Every record the athlete currently holds, flat.</summary>
public class GetPersonalRecordsEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<IReadOnlyList<PersonalRecordView>>
{
    private readonly IPersonalRecordService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetPersonalRecordsEndpoint(IPersonalRecordService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/personal-records")]
    [EndpointDescription("Every personal record the athlete currently holds, newest first.")]
    [ProducesResponseType<IReadOnlyList<PersonalRecordView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<IReadOnlyList<PersonalRecordView>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.GetStandingAsync(user, cancellationToken));
    }
}
