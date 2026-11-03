using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Routines;
using lionheart.Services.Training.Routines;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Routines;

/// <summary>One routine, with the last time it was trained.</summary>
[ValidateModel]
public class GetRoutineEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<RoutineView>
{
    private readonly IRoutineService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetRoutineEndpoint(IRoutineService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/routines/{id:guid}")]
    [EndpointDescription("Get a routine by id.")]
    [ProducesResponseType<RoutineView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<RoutineView>> HandleAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetAsync(user, id, cancellationToken),
            StatusCodes.Status200OK);
    }
}
