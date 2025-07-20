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
    public class GetEquipmentsEndpoint : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<List<Equipment>>
    {
        private readonly IMovementService _movementService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetEquipmentsEndpoint(IMovementService movementService, UserManager<IdentityUser> userManager)
        {
            _movementService = movementService;
            _userManager = userManager;
        }

        [HttpGet("api/equipment/get-all")]
        [EndpointDescription("Get all available equipment.")]
        [ProducesResponseType<List<Equipment>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<List<Equipment>>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _movementService.GetEquipmentsAsync(user));
        }
    }
}
