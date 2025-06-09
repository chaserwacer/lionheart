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
    [ValidateModel]
    public class AddRunWalkActivityEndpoint : EndpointBaseAsync
        .WithRequest<CreateRunWalkRequest>
        .WithActionResult<Activity>
    {
        private readonly IActivityService _activityService;
        private readonly UserManager<IdentityUser> _userManager;

        public AddRunWalkActivityEndpoint(IActivityService activityService, UserManager<IdentityUser> userManager)
        {
            _activityService = activityService;
            _userManager = userManager;
        }

        [HttpPost("api/activity/add-runwalk-activity")]
        [EndpointDescription("Add a new run/walk activity for a user.")]
        [ProducesResponseType<Activity>(StatusCodes.Status201Created)]
        [ProducesResponseType<Activity>(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<Activity>> HandleAsync([FromBody] CreateRunWalkRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _activityService.AddRunWalkActivityAsync(user, request));
        }
    }
}