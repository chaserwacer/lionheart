using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.TrainingSessionEndpoints
{
    [ValidateModel]
    public class DeleteTrainingSessionEndpoint : EndpointBaseAsync
        .WithRequest<Guid>
        .WithActionResult
    {
        private readonly ITrainingSessionService _trainingSessionService;
        private readonly UserManager<IdentityUser> _userManager;

        public DeleteTrainingSessionEndpoint(ITrainingSessionService trainingSessionService, UserManager<IdentityUser> userManager)
        {
            _trainingSessionService = trainingSessionService;
            _userManager = userManager;
        }

        [HttpDelete("api/training-session/delete/{trainingSessionId}")]
        [EndpointDescription("Delete a training session by ID.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult> HandleAsync([FromRoute] Guid trainingSessionId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _trainingSessionService.DeleteTrainingSessionAsync(user, trainingSessionId));
        }
    }
}