using Ardalis.ApiEndpoints;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.InjuryEndpoints;

[ValidateModel]
public class DeleteInjuryEventEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult
{
    private readonly IInjuryService _injuryService;
    private readonly UserManager<IdentityUser> _userManager;

    public DeleteInjuryEventEndpoint(IInjuryService injuryService, UserManager<IdentityUser> userManager)
    {
        _injuryService = injuryService;
        _userManager = userManager;
    }

    [HttpDelete("api/injury/event/{injuryEventId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult> HandleAsync([FromRoute] Guid injuryEventId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var result = await _injuryService.DeleteInjuryEventAsync(user, injuryEventId);
        return this.ToActionResult(result);
    }
}
