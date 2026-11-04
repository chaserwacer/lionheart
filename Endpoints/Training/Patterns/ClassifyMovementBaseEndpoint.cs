using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Patterns;
using lionheart.Services.Training.Patterns;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Patterns;

/// <summary>States what a movement base trains, in place of the map file's entry for it.</summary>
[ValidateModel]
public class ClassifyMovementBaseEndpoint : EndpointBaseAsync
    .WithRequest<Addressed<ClassifyMovementRequest>>
    .WithActionResult<MovementBaseDTO>
{
    private readonly IMovementClassificationService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public ClassifyMovementBaseEndpoint(IMovementClassificationService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPut("api/movement-bases/{id:guid}/classification")]
    [EndpointDescription("State a movement base's pattern and the muscle groups it trains, primary and secondary.")]
    [ProducesResponseType<MovementBaseDTO>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<MovementBaseDTO>> HandleAsync(
        Addressed<ClassifyMovementRequest> request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.ClassifyAsync(user, request.Id, request.Body, cancellationToken),
            StatusCodes.Status200OK);
    }
}
