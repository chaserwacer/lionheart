using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Routines;
using lionheart.Services.Training.Routines;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Routines;

/// <summary>Every routine the athlete has, oldest first, each with the last time it was trained.</summary>
[ValidateModel]
public class GetRoutinesEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<IReadOnlyList<RoutineView>>
{
    private readonly IRoutineService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetRoutinesEndpoint(IRoutineService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/routines")]
    [EndpointDescription("Get every routine.")]
    [ProducesResponseType<IReadOnlyList<RoutineView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<IReadOnlyList<RoutineView>>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetAllAsync(user, cancellationToken),
            StatusCodes.Status200OK);
    }
}
