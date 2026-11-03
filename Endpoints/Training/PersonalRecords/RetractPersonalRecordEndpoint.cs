using Ardalis.ApiEndpoints;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.PersonalRecords;

/// <summary>
/// Withdraws the standing record on a subject, because the set behind it was not real.
/// </summary>
/// <remarks>
/// A transition on the record rather than a shape written over it, so the route reads
/// <c>{id}/retract</c> the way a session reads <c>{id}/complete</c>. Answering with a
/// discriminated outcome rather than a nullable record is what lets a client tell "your previous
/// record stands again" from "this subject now holds no record of that kind".
/// </remarks>
public class RetractPersonalRecordEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<RetractionOutcome>
{
    private readonly IPersonalRecordService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public RetractPersonalRecordEndpoint(IPersonalRecordService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/personal-records/{personalRecordId:guid}/retract")]
    [EndpointDescription("Withdraw the standing record, putting back whatever it beat.")]
    [ProducesResponseType<RetractionOutcome>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<RetractionOutcome>> HandleAsync(
        [FromRoute] Guid personalRecordId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.RetractAsync(user, personalRecordId, cancellationToken));
    }
}
