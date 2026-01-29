using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.Training;

namespace lionheart.Endpoints.Training.Movement.Modifier
{
    [ValidateModel]
    public class GetMovementModifiersEndpoint : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<List<MovementModifierDTO>>
    {
        private readonly IMovementModifierService _movementModifierService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetMovementModifiersEndpoint(IMovementModifierService movementModifierService, UserManager<IdentityUser> userManager)
        {
            _movementModifierService = movementModifierService;
            _userManager = userManager;
        }

        [HttpGet("api/movement-modifier/get-all")]
        [EndpointDescription("Get all movement modifiers for the current user.")]
        [ProducesResponseType<List<MovementModifierDTO>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<List<MovementModifierDTO>>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _movementModifierService.GetMovementModifiersAsync(user));
        }
    }
}
