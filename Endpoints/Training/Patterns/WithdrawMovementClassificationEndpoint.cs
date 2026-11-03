using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Services.Training.Patterns;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Patterns;

/// <summary>Withdraws the athlete's classification of a base, so the map file's entry answers again.</summary>
[ValidateModel]
public class WithdrawMovementClassificationEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<MovementBaseDTO>
{
    private readonly IMovementClassificationService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public WithdrawMovementClassificationEndpoint(IMovementClassificationService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpDelete("api/movement-bases/{id:guid}/classification")]
    [EndpointDescription("Withdraw the athlete's classification of a movement base; the shipped map answers for it again.")]
    [ProducesResponseType<MovementBaseDTO>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<MovementBaseDTO>> HandleAsync(
        [FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.WithdrawAsync(user, id, cancellationToken), StatusCodes.Status200OK);
    }
}
