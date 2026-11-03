using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Focus;
using lionheart.Services.Training.Focus;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Focus;

/// <summary>
/// Lets a focus go, keeping it as history. A transition rather than a field written over, so the
/// route reads <c>{id}/unpin</c> the way a program reads <c>{id}/complete</c>.
/// </summary>
[ValidateModel]
public class UnpinFocusPinEndpoint : EndpointBaseAsync
    .WithRequest<Addressed<UnpinFocusRequest>>
    .WithActionResult<FocusPinView>
{
    private readonly IFocusPinService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public UnpinFocusPinEndpoint(IFocusPinService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/training/focus/{id:guid}/unpin")]
    [EndpointDescription("Unpin a focus on the day given. The pin is kept as history.")]
    [ProducesResponseType<FocusPinView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<FocusPinView>> HandleAsync(
        Addressed<UnpinFocusRequest> request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.UnpinAsync(user, request.Id, request.Body, cancellationToken));
    }
}
