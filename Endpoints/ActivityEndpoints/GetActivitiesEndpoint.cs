using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.DTOs;
using lionheart.ActivityTracking;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;


namespace lionheart.Endpoints.ActivityEndpoints
{
    [TranslateResultToActionResult]
    [ValidateModel]
    public class GetActivitiesEndpoint : EndpointBaseAsync
        .WithRequest<DateRangeRequest>
        .WithActionResult<List<Activity>>
    {
        private readonly IActivityService _activityService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetActivitiesEndpoint(IActivityService activityService, UserManager<IdentityUser> userManager)
        {
            _activityService = activityService;
            _userManager = userManager;
        }

        [HttpGet("api/activity/get-activities")]
        [EndpointDescription("Get all activities for a user within a specified date range.")]
        [ProducesResponseType<List<Activity>>( StatusCodes.Status200OK)]
        [ProducesResponseType<List<Activity>>( StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<List<Activity>>> HandleAsync([FromQuery] DateRangeRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _activityService.GetActivitiesAsync(user, request));

        }
    }
}