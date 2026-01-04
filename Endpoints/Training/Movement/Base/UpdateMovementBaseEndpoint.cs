using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.DTOs;
using lionheart.Model.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Services.Training;

namespace lionheart.Endpoints.Training.Movement
{
    [ValidateModel]
    public class UpdateMovementBaseEndpoint : EndpointBaseAsync
        .WithRequest<UpdateMovementBaseRequest>
        .WithActionResult<MovementBaseDTO>
    {
        private readonly IMovementBaseService _movementBaseService;
        private readonly UserManager<IdentityUser> _userManager;

        public UpdateMovementBaseEndpoint(IMovementBaseService movementBaseService, UserManager<IdentityUser> userManager)
        {
            _movementBaseService = movementBaseService;
            _userManager = userManager;
        }

        [HttpPost("api/movement-base/update")]
        [EndpointDescription("Update an existing movement base.")]
        [ProducesResponseType<MovementBaseDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]

        public override async Task<ActionResult<MovementBaseDTO>> HandleAsync([FromBody] UpdateMovementBaseRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _movementBaseService.UpdateMovementBaseAsync(user, request));
        }
    }
}