using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.TrainingProgram;

namespace lionheart.Endpoints.MovementEndpoints
{
    [ValidateModel]
    public class CreateEquipmentEndpoint : EndpointBaseAsync
        .WithRequest<CreateEquipmentRequest>
        .WithActionResult<Equipment>
    {
        private readonly IMovementService _movementService;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateEquipmentEndpoint(IMovementService movementService, UserManager<IdentityUser> userManager)
        {
            _movementService = movementService;
            _userManager = userManager;
        }

        [HttpPost("api/equipment/create")]
        [EndpointDescription("Create a new equipment.")]
        [ProducesResponseType<Equipment>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public override async Task<ActionResult<Equipment>> HandleAsync([FromBody] CreateEquipmentRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _movementService.CreateEquipmentAsync(user, request));
        }
    }
}
