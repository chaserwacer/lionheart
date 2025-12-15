using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Services.Training;

namespace lionheart.Endpoints.Training.Movement
{
    [ValidateModel]
    public class DeleteMovementBaseEndpoint : EndpointBaseAsync
        .WithRequest<Guid>
        .WithActionResult
    {
        private readonly IMovementBaseService _movementBaseService;
        private readonly UserManager<IdentityUser> _userManager;

        public DeleteMovementBaseEndpoint(IMovementBaseService movementBaseService, UserManager<IdentityUser> userManager)
        {
            _movementBaseService = movementBaseService;
            _userManager = userManager;
        }

        [HttpDelete("api/movement-base/delete/{movementBaseId}")]
        [EndpointDescription("Delete a movement base by ID.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public override async Task<ActionResult> HandleAsync([FromRoute] Guid movementBaseId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized("User is not recognized or no longer exists.");

            return this.ToActionResult  (
                await _movementBaseService.DeleteMovementBaseAsync(user, movementBaseId)
            );
        }
    }
}
