using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.MovementData;

/// <summary>Replaces how the athlete sets a movement up: its cues, and whether its weight adds to bodyweight.</summary>
[ValidateModel]
public class UpdateMovementSetupEndpoint : EndpointBaseAsync
    .WithRequest<Addressed<MovementSetupRequest>>
    .WithActionResult<MovementDataDTO>
{
    private readonly IMovementDataService _movementDataService;
    private readonly UserManager<IdentityUser> _userManager;

    public UpdateMovementSetupEndpoint(IMovementDataService movementDataService, UserManager<IdentityUser> userManager)
    {
        _movementDataService = movementDataService;
        _userManager = userManager;
    }

    [HttpPut("api/movement-datas/{id:guid}/setup")]
    [EndpointDescription("Replace a movement's setup notes and whether its logged weight is added to bodyweight.")]
    [ProducesResponseType<MovementDataDTO>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<MovementDataDTO>> HandleAsync(
        Addressed<MovementSetupRequest> request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _movementDataService.UpdateSetupAsync(user, request.Id, request.Body, cancellationToken),
            StatusCodes.Status200OK);
    }
}
