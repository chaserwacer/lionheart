using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.MovementEndpoints
{
    [ValidateModel]
    public class GetMovementsEndpoint : EndpointBaseAsync
        .WithRequest<Guid>
        .WithActionResult<List<MovementDTO>>
    {
        private readonly IMovementService _movementService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetMovementsEndpoint(IMovementService movementService, UserManager<IdentityUser> userManager)
        {
            _movementService = movementService;
            _userManager = userManager;
        }

        [HttpGet("api/movement/get-all/{sessionId}")]
        [EndpointDescription("Get all movements for a specific training session.")]
        [ProducesResponseType<List<Movement>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<List<MovementDTO>>> HandleAsync([FromRoute] Guid sessionID, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _movementService.GetMovementsAsync(user, sessionID));
        }
    }
}