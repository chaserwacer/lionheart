using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Services.Training;

namespace lionheart.Endpoints.Training.Movement
{
    [ValidateModel]
    public class CreateMovementBaseEndpoint : EndpointBaseAsync
        .WithRequest<CreateMovementBaseRequest>
        .WithActionResult<MovementBaseDTO>
    {
        private readonly IMovementBaseService _movementBaseService;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateMovementBaseEndpoint(IMovementBaseService movementBaseService, UserManager<IdentityUser> userManager)
        {
            _movementBaseService = movementBaseService;
            _userManager = userManager;
        }

        [HttpPost("api/movement-base/create")]
        [EndpointDescription("Create a new movement base.")]
        [ProducesResponseType<MovementBaseDTO>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public override async Task<ActionResult<MovementBaseDTO>> HandleAsync([FromBody] CreateMovementBaseRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _movementBaseService.CreateMovementBaseAsync(user, request));
        }
    }
}