using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.MovementEndpoints
{
    [ValidateModel]
    public class DeleteMovementEndpoint : EndpointBaseAsync
        .WithRequest<Guid>
        .WithActionResult
    {
        private readonly IMovementService _movementService;
        private readonly UserManager<IdentityUser> _userManager;

        public DeleteMovementEndpoint(IMovementService movementService, UserManager<IdentityUser> userManager)
        {
            _movementService = movementService;
            _userManager = userManager;
        }

        [HttpDelete("api/movement/delete/{movementId}")]
        [EndpointDescription("Delete a movement by ID.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult> HandleAsync([FromRoute] Guid movementId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _movementService.DeleteMovementAsync(user, movementId));
        }
    }
}