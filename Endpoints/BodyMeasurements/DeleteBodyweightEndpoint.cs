using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Model.BodyMeasurements;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.BodyMeasurements;

/// <summary>Deletes the bodyweight measured on a day.</summary>
[ValidateModel]
public class DeleteBodyweightEndpoint : EndpointBaseAsync
    .WithRequest<DateOnly>
    .WithActionResult
{
    private readonly IBodyweightService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public DeleteBodyweightEndpoint(IBodyweightService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpDelete("api/bodyweight/{day:datetime}")]
    [EndpointDescription("Delete the bodyweight measured on a day.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult> HandleAsync([FromRoute] DateOnly day, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.DeleteAsync(user, day, cancellationToken));
    }
}
