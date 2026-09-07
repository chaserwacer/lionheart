using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Model.Contexts;
using lionheart.Services.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Contexts;

/// <summary>Every context whose period starts inside a window.</summary>
[ValidateModel]
public class GetContextsEndpoint : EndpointBaseAsync
    .WithRequest<TimeWindow>
    .WithActionResult<IReadOnlyList<ContextView>>
{
    private readonly IContextService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetContextsEndpoint(IContextService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/contexts")]
    [EndpointDescription("Get the contexts falling inside a time window.")]
    [ProducesResponseType<IReadOnlyList<ContextView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public override async Task<ActionResult<IReadOnlyList<ContextView>>> HandleAsync([FromQuery] TimeWindow request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetWindowAsync(user, request, cancellationToken),
            StatusCodes.Status200OK);
    }
}
