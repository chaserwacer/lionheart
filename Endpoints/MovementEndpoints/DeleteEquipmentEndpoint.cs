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
    public class DeleteEquipmentEndpoint : EndpointBaseAsync
        .WithRequest<Guid>
        .WithActionResult
    {
        private readonly IMovementService _movementService;
        private readonly UserManager<IdentityUser> _userManager;

        public DeleteEquipmentEndpoint(IMovementService movementService, UserManager<IdentityUser> userManager)
        {
            _movementService = movementService;
            _userManager = userManager;
        }

        [HttpDelete("api/equipment/delete/{equipmentId}")]
        [EndpointDescription("Delete an equipment by ID.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public override async Task<ActionResult> HandleAsync([FromRoute] Guid equipmentId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized("User is not recognized or no longer exists.");

            return this.ToActionResult(
                await _movementService.DeleteEquipmentAsync(user, equipmentId)
            );
        }
    }
}
