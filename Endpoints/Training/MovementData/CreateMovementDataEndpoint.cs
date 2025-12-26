using Ardalis.ApiEndpoints;
using Ardalis.Filters;
using Ardalis.Result.AspNetCore;
using lionheart.Model.Training;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.MovementData;

[ValidateModel]
public class CreateMovementDataEndpoint : EndpointBaseAsync
    .WithRequest<CreateMovementDataRequest>
    .WithActionResult<MovementDataDTO>
{
    private readonly IMovementDataService _movementDataService;
    private readonly UserManager<IdentityUser> _userManager;

    public CreateMovementDataEndpoint(IMovementDataService movementDataService, UserManager<IdentityUser> userManager)
    {
        _movementDataService = movementDataService;
        _userManager = userManager;
    }

    [HttpPost("api/movement-data/create")]
    [EndpointDescription("Create a new movement data configuration. Returns conflict if the combination already exists.")]
    [ProducesResponseType<MovementDataDTO>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<MovementDataDTO>> HandleAsync([FromBody] CreateMovementDataRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

        return this.ToActionResult(await _movementDataService.CreateMovementDataAsync(user, request));
    }
}
