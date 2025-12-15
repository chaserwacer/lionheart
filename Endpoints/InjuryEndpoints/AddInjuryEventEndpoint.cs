using Ardalis.ApiEndpoints;
using lionheart.Model.DTOs;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.Injury;

namespace lionheart.Endpoints.InjuryEndpoints
{
    [ValidateModel]
    public class CreateInjuryEventEndpoint : EndpointBaseAsync
        .WithRequest<CreateInjuryEventRequest>
        .WithActionResult<InjuryDTO>
    {
        private readonly IInjuryService _injuryService;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateInjuryEventEndpoint(IInjuryService injuryService, UserManager<IdentityUser> userManager)
        {
            _injuryService = injuryService;
            _userManager = userManager;
        }

        // Keep route for backward compatibility
        [HttpPost("api/injury/add-event")]
        [ProducesResponseType(typeof(InjuryDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<InjuryDTO>> HandleAsync(
            [FromBody] CreateInjuryEventRequest request,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized();

            var result = await _injuryService.CreateInjuryEventAsync(user, request);
            return this.ToActionResult(result);
        }
    }
}
