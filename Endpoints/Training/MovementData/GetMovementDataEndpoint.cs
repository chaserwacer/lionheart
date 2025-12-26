using Ardalis.ApiEndpoints;
using Ardalis.Result.AspNetCore;
using lionheart.Model.Training;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.MovementData;

public class GetMovementDataEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<MovementDataDTO>
{
    private readonly IMovementDataService _movementDataService;
    private readonly UserManager<IdentityUser> _userManager;

    public GetMovementDataEndpoint(IMovementDataService movementDataService, UserManager<IdentityUser> userManager)
    {
        _movementDataService = movementDataService;
        _userManager = userManager;
    }

    [HttpGet("api/movement-data/{movementDataId:guid}")]
    [EndpointDescription("Get a specific movement data by ID.")]
    [ProducesResponseType<MovementDataDTO>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<MovementDataDTO>> HandleAsync([FromRoute] Guid movementDataId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

        return this.ToActionResult(await _movementDataService.GetMovementDataAsync(user, movementDataId));
    }
}
