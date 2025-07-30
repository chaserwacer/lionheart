using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Model.DTOs;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using System.ComponentModel.DataAnnotations;

namespace lionheart.Endpoints.InjuryEndpoints;

[ValidateModel]
public class AddInjuryEventEndpoint : EndpointBaseAsync
    .WithRequest<AddInjuryEventWrapper>
    .WithActionResult<InjuryDTO>
{
    private readonly IInjuryService _injuryService;
    private readonly UserManager<IdentityUser> _userManager;

    public AddInjuryEventEndpoint(IInjuryService injuryService, UserManager<IdentityUser> userManager)
    {
        _injuryService = injuryService;
        _userManager = userManager;
    }

    [HttpPost("api/injury/add-event")]
    [ProducesResponseType(typeof(InjuryDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<InjuryDTO>> HandleAsync(
        [FromBody] AddInjuryEventWrapper wrapper,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        return this.ToActionResult(
            await _injuryService.AddInjuryEventAsync(user, wrapper.InjuryId, wrapper.Request));
    }
}
