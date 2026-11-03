using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Services.Training.Focus;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Focus;

/// <summary>Deletes a pin made by mistake. A focus the athlete let go is unpinned instead.</summary>
[ValidateModel]
public class DeleteFocusPinEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult
{
    private readonly IFocusPinService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public DeleteFocusPinEndpoint(IFocusPinService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpDelete("api/training/focus/{id:guid}")]
    [EndpointDescription("Delete a focus pin made by mistake.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult> HandleAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.DeleteAsync(user, id, cancellationToken));
    }
}
