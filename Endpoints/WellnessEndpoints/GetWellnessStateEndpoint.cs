using Ardalis.ApiEndpoints;
using lionheart.WellBeing;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.WellnessEndpoints
{
    [ValidateModel]
    public class GetWellnessStateEndpoint : EndpointBaseAsync
        .WithRequest<DateOnly>
        .WithActionResult<WellnessState>
    {
        private readonly IWellnessService _wellnessService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetWellnessStateEndpoint(IWellnessService wellnessService, UserManager<IdentityUser> userManager)
        {
            _wellnessService = wellnessService;
            _userManager = userManager;
        }

        [HttpGet("api/wellness/get")]
        [EndpointDescription("Fetch the WellnessState for the user on a specific date.")]
        [ProducesResponseType<WellnessState>(StatusCodes.Status200OK)]
        [ProducesResponseType<WellnessState>(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<WellnessState>> HandleAsync([FromQuery] DateOnly date, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _wellnessService.GetWellnessStateAsync(user, date));
        }
    }
}