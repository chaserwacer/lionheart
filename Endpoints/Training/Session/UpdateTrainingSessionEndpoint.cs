using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.Training;

namespace lionheart.Endpoints.TrainingSessionEndpoints
{
    [ValidateModel]
    public class UpdateTrainingSessionEndpoint : EndpointBaseAsync
        .WithRequest<UpdateTrainingSessionRequest>
        .WithActionResult<TrainingSessionDTO>
    {
        private readonly ITrainingSessionService _trainingSessionService;
        private readonly UserManager<IdentityUser> _userManager;

        public UpdateTrainingSessionEndpoint(ITrainingSessionService trainingSessionService, UserManager<IdentityUser> userManager)
        {
            _trainingSessionService = trainingSessionService;
            _userManager = userManager;
        }

        [HttpPut("api/training-session/update")]
        [EndpointDescription("Update an existing training session.")]
        [ProducesResponseType<TrainingSessionDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<TrainingSessionDTO>> HandleAsync([FromBody] UpdateTrainingSessionRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _trainingSessionService.UpdateTrainingSessionAsync(user, request));
        }
    }
}