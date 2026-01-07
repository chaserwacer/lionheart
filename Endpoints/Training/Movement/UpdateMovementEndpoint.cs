using Ardalis.ApiEndpoints;
using lionheart.Model.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.Training.Movement
{
    [ValidateModel]
    public class UpdateMovementEndpoint : EndpointBaseAsync
        .WithRequest<UpdateMovementRequest>
        .WithActionResult<MovementDTO>
    {
        private readonly IMovementService _movementService;
        private readonly UserManager<IdentityUser> _userManager;

        public UpdateMovementEndpoint(IMovementService movementService, UserManager<IdentityUser> userManager)
        {
            _movementService = movementService;
            _userManager = userManager;
        }

        [HttpPut("api/movement/update")]
        [EndpointDescription("Update an existing movement.")]
        [ProducesResponseType<MovementDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<MovementDTO>> HandleAsync([FromBody] UpdateMovementRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _movementService.UpdateMovementAsync(user, request));
        }
    }
}