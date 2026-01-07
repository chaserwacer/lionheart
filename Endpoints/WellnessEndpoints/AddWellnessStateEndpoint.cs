using Ardalis.ApiEndpoints;
using lionheart.Services;
using lionheart.ActivityTracking;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.WellBeing;

namespace lionheart.Endpoints.WellnessEndpoints
{
    [ValidateModel]
    public class AddWellnessStateEndpoint : EndpointBaseAsync
        .WithRequest<CreateWellnessStateRequest>
        .WithActionResult<WellnessState>
    {
        private readonly IWellnessService _wellnessService;
        private readonly UserManager<IdentityUser> _userManager;

        public AddWellnessStateEndpoint(IWellnessService wellnessService, UserManager<IdentityUser> userManager)
        {
            _wellnessService = wellnessService;
            _userManager = userManager;
        }

        [HttpPost("api/wellness/add")]
        [EndpointDescription("Add/update a wellness state for a user.")]
        [ProducesResponseType<Activity>(StatusCodes.Status201Created)]
        [ProducesResponseType<Activity>(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<WellnessState>> HandleAsync([FromBody] CreateWellnessStateRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _wellnessService.AddWellnessStateAsync(user, request));
        }
    }
}