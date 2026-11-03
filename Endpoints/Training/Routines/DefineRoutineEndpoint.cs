using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Routines;
using lionheart.Services.Training.Routines;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Routines;

/// <summary>Defines a routine: a named order of movements, each with its working sets.</summary>
[ValidateModel]
public class DefineRoutineEndpoint : EndpointBaseAsync
    .WithRequest<DefineRoutineRequest>
    .WithActionResult<RoutineView>
{
    private readonly IRoutineService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public DefineRoutineEndpoint(IRoutineService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/training/routines")]
    [EndpointDescription("Define a routine.")]
    [ProducesResponseType<RoutineView>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<RoutineView>> HandleAsync([FromBody] DefineRoutineRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.DefineAsync(user, request, cancellationToken),
            StatusCodes.Status201Created);
    }
}
