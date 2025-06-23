using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using System.Threading;
using System.Threading.Tasks;

namespace lionheart.Endpoints.MovementEndpoints
{
    [ValidateModel]
    public class UpdateMovementOrderEndpoint : EndpointBaseAsync
        .WithRequest<UpdateMovementOrderRequest>
        .WithActionResult
    {
        private readonly IMovementService _movementService;
        private readonly UserManager<IdentityUser> _userManager;

        public UpdateMovementOrderEndpoint(IMovementService movementService, UserManager<IdentityUser> userManager)
        {
            _movementService = movementService;
            _userManager = userManager;
        }

        [HttpPut("api/movement/update-order")]
        [EndpointDescription("Update the order of movements in a training session.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult> HandleAsync([FromBody] UpdateMovementOrderRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _movementService.UpdateMovementOrder(user, request));
        }
    }
}
