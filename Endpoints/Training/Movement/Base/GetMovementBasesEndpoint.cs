using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using ModelContextProtocol.Server;
using System.ComponentModel;
using lionheart.Services.Training;

namespace lionheart.Endpoints.Training.Movement
{
    [ValidateModel]
    public class GetMovementBasesEndpoint : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<List<MovementBaseDTO>>
    {
        private readonly IMovementBaseService _movementBaseService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetMovementBasesEndpoint(IMovementBaseService movementBaseService, UserManager<IdentityUser> userManager)
        {
            _movementBaseService = movementBaseService;
            _userManager = userManager;
        }

        [HttpGet("api/movement-base/get-all")]
        [EndpointDescription("Get all available movement bases.")]
        [ProducesResponseType<List<MovementBaseDTO>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<List<MovementBaseDTO>>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _movementBaseService.GetMovementBasesAsync(user));
        }
    }
}