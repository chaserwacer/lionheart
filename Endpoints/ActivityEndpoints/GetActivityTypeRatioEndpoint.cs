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
    public class GetActivityTypeRatioEndpoint : EndpointBaseAsync
        .WithRequest<DateRangeRequest>
        .WithActionResult<ActivityTypeRatioDto>
    {
        private readonly IActivityService _activityService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetActivityTypeRatioEndpoint(IActivityService activityService, UserManager<IdentityUser> userManager)
        {
            _activityService = activityService;
            _userManager = userManager;
        }

        [HttpGet("api/activity/get-activity-type-ratio")]
        [EndpointDescription("Get the ratio of different activity types for a user within a specified date range.")]
        [ProducesResponseType<ActivityTypeRatioDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
        public override async Task<ActionResult<ActivityTypeRatioDto>> HandleAsync([FromQuery] DateRangeRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _activityService.GetActivityTypeRatioAsync(user, request));
        }
    }
}