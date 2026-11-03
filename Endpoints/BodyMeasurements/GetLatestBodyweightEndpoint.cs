using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Model.BodyMeasurements;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.BodyMeasurements;

/// <summary>The newest bodyweight measured.</summary>
[ValidateModel]
public class GetLatestBodyweightEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<BodyweightView>
{
    private readonly IBodyweightService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetLatestBodyweightEndpoint(IBodyweightService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/bodyweight/latest")]
    [EndpointDescription("Get the most recent bodyweight measurement.")]
    [ProducesResponseType<BodyweightView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<BodyweightView>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetLatestAsync(user, cancellationToken),
            StatusCodes.Status200OK);
    }
}
