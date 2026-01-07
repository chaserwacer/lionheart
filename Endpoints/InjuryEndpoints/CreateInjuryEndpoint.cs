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
    public class CreateInjuryEndpoint : EndpointBaseAsync
        .WithRequest<CreateInjuryRequest>
        .WithActionResult<InjuryDTO>
    {
        private readonly IInjuryService _injuryService;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateInjuryEndpoint(IInjuryService injuryService, UserManager<IdentityUser> userManager)
        {
            _injuryService = injuryService;
            _userManager = userManager;
        }

        [HttpPost("api/injury/create")]
        [ProducesResponseType<InjuryDTO>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<InjuryDTO>> HandleAsync([FromBody] CreateInjuryRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized();

            return this.ToActionResult(await _injuryService.CreateInjuryAsync(user, request));
        }
    }
}
