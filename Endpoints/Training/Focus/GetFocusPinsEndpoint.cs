using Ardalis.ApiEndpoints;
using lionheart.Model.Training.Focus;
using lionheart.Services.Training.Focus;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Focus;

/// <summary>The athlete's whole focus list, the pins still meant first.</summary>
public class GetFocusPinsEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<IReadOnlyList<FocusPinView>>
{
    private readonly IFocusPinService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetFocusPinsEndpoint(IFocusPinService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/focus")]
    [EndpointDescription("Every focus pin the athlete has made: pinned first, then by horizon start, latest first.")]
    [ProducesResponseType<IReadOnlyList<FocusPinView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<IReadOnlyList<FocusPinView>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.GetAllAsync(user, cancellationToken));
    }
}
