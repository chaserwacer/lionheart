using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.TrainingSessionEndpoints
{
    [ValidateModel]
    public class GetTrainingSessionEndpoint : EndpointBaseAsync
        .WithRequest<Guid>
        .WithActionResult<TrainingSessionDTO>
    {
        private readonly ITrainingSessionService _trainingSessionService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetTrainingSessionEndpoint(ITrainingSessionService trainingSessionService, UserManager<IdentityUser> userManager)
        {
            _trainingSessionService = trainingSessionService;
            _userManager = userManager;
        }

        [HttpGet("api/training-session/get/{trainingSessionId}")]
        [EndpointDescription("Get a specific training session by ID.")]
        [ProducesResponseType<TrainingSessionDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<TrainingSessionDTO>> HandleAsync([FromRoute] Guid trainingSessionId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _trainingSessionService.GetTrainingSessionAsync(user, trainingSessionId));
        }
    }
}