using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.ActivityEndpoints
{
    [ValidateModel]
    public class GetMuscleSetsEndpoint : EndpointBaseAsync
        .WithRequest<DateRangeRequest>
        .WithActionResult<MuscleSetsDto>
    {
        private readonly IActivityService _activityService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetMuscleSetsEndpoint(IActivityService activityService, UserManager<IdentityUser> userManager)
        {
            _activityService = activityService;
            _userManager = userManager;
        }

        [HttpGet("api/activity/get-muscle-sets")]
        [EndpointDescription("Get the muscle sets for a user within a specified date range.")]
        [ProducesResponseType<MuscleSetsDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<MuscleSetsDto>(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<MuscleSetsDto>> HandleAsync([FromQuery] DateRangeRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _activityService.GetMuscleSetsAsync(user, request));
        }
    }
}