using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.DTOs;
using lionheart.Model.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.Training.Movement
{
    [ValidateModel]
    public class CreateMovementEndpoint : EndpointBaseAsync
        .WithRequest<CreateMovementRequest>
        .WithActionResult<MovementDTO>
    {
        private readonly IMovementService _movementService;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateMovementEndpoint(IMovementService movementService, UserManager<IdentityUser> userManager)
        {
            _movementService = movementService;
            _userManager = userManager;
        }

        [HttpPost("api/movement/create")]
        [EndpointDescription("Create a new movement within a training session.")]
        [ProducesResponseType<MovementDTO>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<MovementDTO>> HandleAsync([FromBody] CreateMovementRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _movementService.CreateMovementAsync(user, request));
        }
    }
}