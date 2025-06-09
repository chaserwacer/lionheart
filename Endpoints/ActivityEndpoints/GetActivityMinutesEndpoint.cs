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
    public class GetActivityMinutesEndpoint : EndpointBaseAsync
        .WithRequest<DateRangeRequest>
        .WithActionResult<int>
    {
        private readonly IActivityService _activityService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetActivityMinutesEndpoint(IActivityService activityService, UserManager<IdentityUser> userManager)
        {
            _activityService = activityService;
            _userManager = userManager;
        }

        [HttpGet("api/activity/get-activity-minutes")]
        [EndpointDescription("Get the total number of activity minutes for a user within a specified date range.")]
        [ProducesResponseType<int>(StatusCodes.Status200OK)]
        [ProducesResponseType<int>(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<int>> HandleAsync([FromQuery] DateRangeRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _activityService.GetActivityMinutesAsync(user, request));
        }
    }
}