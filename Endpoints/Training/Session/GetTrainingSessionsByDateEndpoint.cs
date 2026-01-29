using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.Training;
using lionheart.Model.Request;

namespace lionheart.Endpoints.TrainingSessionEndpoints
{
    [ValidateModel]
    public class GetTrainingSessionsByDateEndpoint : EndpointBaseAsync
        .WithRequest<DateRangeRequest>
        .WithActionResult<List<TrainingSessionDTO>>
    {
        private readonly ITrainingSessionService _trainingSessionService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetTrainingSessionsByDateEndpoint(ITrainingSessionService trainingSessionService, UserManager<IdentityUser> userManager)
        {
            _trainingSessionService = trainingSessionService;
            _userManager = userManager;
        }

        [HttpPost("api/training-session/get-date-range")]
        [EndpointDescription("Get all training sessions within a specified date range.")]
        [ProducesResponseType<List<TrainingSessionDTO>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<List<TrainingSessionDTO>>> HandleAsync([FromBody] DateRangeRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _trainingSessionService.GetTrainingSessionsAsync(user, request));
        }
    }
}