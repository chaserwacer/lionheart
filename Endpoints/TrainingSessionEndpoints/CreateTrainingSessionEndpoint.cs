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
    public class CreateTrainingSessionEndpoint : EndpointBaseAsync
        .WithRequest<CreateTrainingSessionRequest>
        .WithActionResult<TrainingSessionDTO>
    {
        private readonly ITrainingSessionService _trainingSessionService;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateTrainingSessionEndpoint(ITrainingSessionService trainingSessionService, UserManager<IdentityUser> userManager)
        {
            _trainingSessionService = trainingSessionService;
            _userManager = userManager;
        }

        [HttpPost("api/training-session/create")]
        [EndpointDescription("Create a new training session within a training program.")]
        [ProducesResponseType<TrainingSessionDTO>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<TrainingSessionDTO>> HandleAsync([FromBody] CreateTrainingSessionRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _trainingSessionService.CreateTrainingSessionAsync(user, request));
        }
    }
}