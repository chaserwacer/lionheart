using Ardalis.ApiEndpoints;
using Ardalis.Result.AspNetCore;
using lionheart.Model.Training;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.MovementData;

public class GetMovementDatasEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<List<MovementDataDTO>>
{
    private readonly IMovementDataService _movementDataService;
    private readonly UserManager<IdentityUser> _userManager;

    public GetMovementDatasEndpoint(IMovementDataService movementDataService, UserManager<IdentityUser> userManager)
    {
        _movementDataService = movementDataService;
        _userManager = userManager;
    }

    [HttpGet("api/movement-datas")]
    [EndpointDescription("Get all movement data configurations for the current user.")]
    [ProducesResponseType<List<MovementDataDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<List<MovementDataDTO>>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

        return this.ToActionResult(await _movementDataService.GetMovementDatasAsync(user));
    }
}
