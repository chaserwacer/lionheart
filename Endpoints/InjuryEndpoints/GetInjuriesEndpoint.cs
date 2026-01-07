using Ardalis.ApiEndpoints;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.InjuryManagement;

namespace lionheart.Endpoints.InjuryEndpoints
{
    [ValidateModel]
    public class GetInjuriesEndpoint : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<List<InjuryDTO>>
    {
        private readonly IInjuryService _injuryService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetInjuriesEndpoint(IInjuryService injuryService, UserManager<IdentityUser> userManager)
        {
            _injuryService = injuryService;
            _userManager = userManager;
        }

        [HttpGet("api/injury/get-user-injuries")]
        [ProducesResponseType<List<InjuryDTO>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<List<InjuryDTO>>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized();

            return this.ToActionResult(await _injuryService.GetUserInjuriesAsync(user));
        }
    }
}
