using Ardalis.ApiEndpoints;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.PersonalRecords;

/// <summary>One movement's standing records.</summary>
public class GetMovementPersonalRecordsEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<SubjectRecordsView>
{
    private readonly IPersonalRecordService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetMovementPersonalRecordsEndpoint(
        IPersonalRecordService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/personal-records/summaries/{movementDataId:guid}")]
    [EndpointDescription("The records the athlete currently holds on one movement.")]
    [ProducesResponseType<SubjectRecordsView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<SubjectRecordsView>> HandleAsync(
        [FromRoute] Guid movementDataId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetForMovementAsync(user, movementDataId, cancellationToken));
    }
}
