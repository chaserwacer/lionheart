using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.InjuryEndpoints;

[ValidateModel]
public class MarkInjuryResolvedEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult
{
    private readonly IInjuryService _injuryService;
    private readonly UserManager<IdentityUser> _userManager;

    public MarkInjuryResolvedEndpoint(IInjuryService injuryService, UserManager<IdentityUser> userManager)
    {
        _injuryService = injuryService;
        _userManager = userManager;
    }

    [HttpPut("api/injury/{injuryId}/resolve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult> HandleAsync([FromRoute] Guid injuryId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        return this.ToActionResult(await _injuryService.MarkInjuryResolvedAsync(user, injuryId));
    }
}
