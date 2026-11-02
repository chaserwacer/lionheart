using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Oura;
using lionheart.Services.Oura;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Oura;

/// <summary>One day's Oura documents, all six collections in one answer.</summary>
[ValidateModel]
public class GetOuraDayEndpoint : EndpointBaseAsync
    .WithRequest<DateOnly>
    .WithActionResult<OuraDayView>
{
    private readonly IOuraReadService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetOuraDayEndpoint(IOuraReadService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/oura/days/{day:datetime}")]
    [EndpointDescription("Get the Oura daily documents for one day.")]
    [ProducesResponseType<OuraDayView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<OuraDayView>> HandleAsync(
        [FromRoute] DateOnly day, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetDayAsync(user, day, cancellationToken),
            StatusCodes.Status200OK);
    }
}
