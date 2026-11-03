using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Routines;
using lionheart.Services.Training.Routines;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Routines;

/// <summary>Restates a routine whole: its name, notes and movements.</summary>
[ValidateModel]
public class ReviseRoutineEndpoint : EndpointBaseAsync
    .WithRequest<Addressed<DefineRoutineRequest>>
    .WithActionResult<RoutineView>
{
    private readonly IRoutineService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public ReviseRoutineEndpoint(IRoutineService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPut("api/training/routines/{id:guid}")]
    [EndpointDescription("Replace a routine's name, notes and movements.")]
    [ProducesResponseType<RoutineView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<RoutineView>> HandleAsync(Addressed<DefineRoutineRequest> request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.ReviseAsync(user, request.Id, request.Body, cancellationToken),
            StatusCodes.Status200OK);
    }
}
