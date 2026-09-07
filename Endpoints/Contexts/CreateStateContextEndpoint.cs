using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Contexts;
using lionheart.Services.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Contexts;

/// <summary>A body or life state with no exertion payload.</summary>
[ValidateModel]
public class CreateStateContextEndpoint : EndpointBaseAsync
    .WithRequest<CreateStateContextRequest>
    .WithActionResult<ContextView>
{
    private readonly IContextService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public CreateStateContextEndpoint(IContextService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/contexts/state")]
    [EndpointDescription("Record a state context.")]
    [ProducesResponseType<ContextView>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public override async Task<ActionResult<ContextView>> HandleAsync([FromBody] CreateStateContextRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.CreateStateAsync(user, request, cancellationToken),
            StatusCodes.Status201Created);
    }
}
