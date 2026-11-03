using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Services.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Contexts;

/// <summary>Deletes a context and the analysis projection derived from it.</summary>
[ValidateModel]
public class DeleteContextEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult
{
    private readonly IContextService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public DeleteContextEndpoint(IContextService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpDelete("api/contexts/{id:guid}")]
    [EndpointDescription("Delete a context.")]
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
