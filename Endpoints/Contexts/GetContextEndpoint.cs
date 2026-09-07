using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Contexts;
using lionheart.Services.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Contexts;

/// <summary>One context, of either kind.</summary>
[ValidateModel]
public class GetContextEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<ContextView>
{
    private readonly IContextService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetContextEndpoint(IContextService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/contexts/{id:guid}")]
    [EndpointDescription("Get a context by id.")]
    [ProducesResponseType<ContextView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<ContextView>> HandleAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetAsync(user, id, cancellationToken),
            StatusCodes.Status200OK);
    }
}
