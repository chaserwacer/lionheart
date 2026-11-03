using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Routines;
using lionheart.Services.Training.Routines;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Routines;

/// <summary>Saves a completed session as a routine, its movements and working sets read off the session.</summary>
[ValidateModel]
public class DefineRoutineFromSessionEndpoint : EndpointBaseAsync
    .WithRequest<Addressed<RoutineFromSessionRequest>>
    .WithActionResult<RoutineView>
{
    private readonly IRoutineService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public DefineRoutineFromSessionEndpoint(IRoutineService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/training/routines/from-session/{id:guid}")]
    [EndpointDescription("Save a completed training session as a routine.")]
    [ProducesResponseType<RoutineView>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<RoutineView>> HandleAsync(Addressed<RoutineFromSessionRequest> request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.FromSessionAsync(user, request.Id, request.Body, cancellationToken),
            StatusCodes.Status201Created);
    }
}
