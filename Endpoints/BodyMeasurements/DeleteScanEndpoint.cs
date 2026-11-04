using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Services.BodyComposition;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.BodyMeasurements;

/// <summary>Deletes a body-composition scan. The bodyweight it measured stays logged.</summary>
[ValidateModel]
public class DeleteScanEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult
{
    private readonly IBodyCompositionService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public DeleteScanEndpoint(IBodyCompositionService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpDelete("api/body-composition/scans/{id:guid}")]
    [EndpointDescription("Delete a body-composition scan; the bodyweight it measured stays logged.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult> HandleAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.DeleteAsync(user, id, cancellationToken));
    }
}
