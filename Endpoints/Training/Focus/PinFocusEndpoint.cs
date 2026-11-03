using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Focus;
using lionheart.Services.Training.Focus;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Focus;

/// <summary>Pins one of the athlete's movements to emphasise over a horizon.</summary>
[ValidateModel]
public class PinFocusEndpoint : EndpointBaseAsync
    .WithRequest<PinFocusRequest>
    .WithActionResult<FocusPinView>
{
    private readonly IFocusPinService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public PinFocusEndpoint(IFocusPinService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/training/focus")]
    [EndpointDescription("Pin a movement to focus on over a horizon, at a weekly frequency, with an optional target.")]
    [ProducesResponseType<FocusPinView>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<FocusPinView>> HandleAsync(
        [FromBody] PinFocusRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.PinAsync(user, request, cancellationToken),
            StatusCodes.Status201Created);
    }
}
