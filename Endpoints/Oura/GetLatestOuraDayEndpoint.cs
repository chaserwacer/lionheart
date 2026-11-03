using Ardalis.ApiEndpoints;
using lionheart.Model.Oura;
using lionheart.Services.Oura;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Oura;

/// <summary>The newest day any Oura collection covers, all six collections in one answer.</summary>
/// <remarks>
/// How a client tells how far behind the store is before it asks for a sync: a range read would
/// have to guess how far back to look, and the store can be months behind.
/// </remarks>
public class GetLatestOuraDayEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<OuraDayView>
{
    private readonly IOuraReadService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetLatestOuraDayEndpoint(IOuraReadService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/oura/days/latest")]
    [EndpointDescription("Get the Oura daily documents for the most recent day any collection covers.")]
    [ProducesResponseType<OuraDayView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<OuraDayView>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetLatestDayAsync(user, cancellationToken),
            StatusCodes.Status200OK);
    }
}
