using Ardalis.ApiEndpoints;
using lionheart.Services;
using lionheart.ActivityTracking;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.Request;

namespace lionheart.Endpoints.Training.Activity
{
    [ValidateModel]
    public class AddActivityEndpoint : EndpointBaseAsync
        .WithRequest<CreateActivityRequest>
        .WithActionResult<ActivityDTO>
    {
        private readonly IActivityService _activityService;
        private readonly UserManager<IdentityUser> _userManager;

        public AddActivityEndpoint(IActivityService activityService, UserManager<IdentityUser> userManager)
        {
            _activityService = activityService;
            _userManager = userManager;
        }

        [HttpPost("api/activity/add-activity")]
        [EndpointDescription("Add a new activity for a user.")]
        [ProducesResponseType<ActivityDTO>(StatusCodes.Status201Created)]
        [ProducesResponseType<ActivityDTO>(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<ActivityDTO>> HandleAsync([FromBody] CreateActivityRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _activityService.AddActivityAsync(user, request));
        }
    }

    [ValidateModel]
    public class UpdateActivityEndpoint : EndpointBaseAsync
        .WithRequest<UpdateActivityRequest>
        .WithActionResult<ActivityDTO>
    {
        private readonly IActivityService _activityService;
        private readonly UserManager<IdentityUser> _userManager;

        public UpdateActivityEndpoint(IActivityService activityService, UserManager<IdentityUser> userManager)
        {
            _activityService = activityService;
            _userManager = userManager;
        }

        [HttpPut("api/activity/update-activity")]
        [EndpointDescription("Update an existing activity for a user.")]
        [ProducesResponseType<ActivityDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType<ActivityDTO>(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<ActivityDTO>> HandleAsync([FromBody] UpdateActivityRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _activityService.UpdateActivityAsync(user, request));
        }
    }

    [ValidateModel]
    public class DeleteActivityEndpoint : EndpointBaseAsync
        .WithRequest<Guid>
        .WithActionResult<bool>
    {
        private readonly IActivityService _activityService;
        private readonly UserManager<IdentityUser> _userManager;

        public DeleteActivityEndpoint(IActivityService activityService, UserManager<IdentityUser> userManager)
        {
            _activityService = activityService;
            _userManager = userManager;
        }

        [HttpDelete("api/activity/delete-activity")]
        [EndpointDescription("Delete an existing activity for a user.")]
        [ProducesResponseType<bool>(StatusCodes.Status200OK)]
        [ProducesResponseType<bool>(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<bool>> HandleAsync([FromBody] Guid request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _activityService.DeleteActivityAsync(user, request));
        }
    }

    [ValidateModel]
    public class GetActivitiesEndpoint : EndpointBaseAsync
        .WithRequest<DateRangeRequest>
        .WithActionResult<List<ActivityDTO>>
    {
        private readonly IActivityService _activityService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetActivitiesEndpoint(IActivityService activityService, UserManager<IdentityUser> userManager)
        {
            _activityService = activityService;
            _userManager = userManager;
        }

        [HttpGet("api/activity/get-user-activities")]
        [EndpointDescription("Get all activities for a specific user.")]
        [ProducesResponseType<List<ActivityDTO>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<List<ActivityDTO>>> HandleAsync([FromQuery] DateRangeRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _activityService.GetActivitiesAsync(user, request));
        }
    }

    [ValidateModel]
    public class GetActivityEndpoint : EndpointBaseAsync
        .WithRequest<Guid>
        .WithActionResult<ActivityDTO>
    {
        private readonly IActivityService _activityService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetActivityEndpoint(IActivityService activityService, UserManager<IdentityUser> userManager)
        {
            _activityService = activityService;
            _userManager = userManager;
        }

        [HttpGet("api/activity/get-activity/{activityId}")]
        [EndpointDescription("Get a specific activity by ID.")]
        [ProducesResponseType<ActivityDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<ActivityDTO>> HandleAsync([FromRoute] Guid activityId, CancellationToken cancellationToken = default)
        {

            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }
            return this.ToActionResult(await _activityService.GetActivityAsync(user, activityId));
        }
    }
}