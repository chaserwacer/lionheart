using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Focus;
using lionheart.Services.Training.Focus;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Focus;

/// <summary>Replaces a pinned focus's horizon, frequency and target.</summary>
[ValidateModel]
public class ReviseFocusPinEndpoint : EndpointBaseAsync
    .WithRequest<Addressed<ReviseFocusRequest>>
    .WithActionResult<FocusPinView>
{
    private readonly IFocusPinService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public ReviseFocusPinEndpoint(IFocusPinService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPatch("api/training/focus/{id:guid}")]
    [EndpointDescription("Revise a pinned focus: its horizon, frequency and target, replaced whole. An unpinned focus is history and cannot be revised.")]
    [ProducesResponseType<FocusPinView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<FocusPinView>> HandleAsync(
        Addressed<ReviseFocusRequest> request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.ReviseAsync(user, request.Id, request.Body, cancellationToken));
    }
}
