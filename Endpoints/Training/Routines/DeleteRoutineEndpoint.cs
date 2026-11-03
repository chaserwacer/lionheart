using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Routines;
using lionheart.Services.Training.Routines;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Routines;

/// <summary>Deletes a routine. Sessions trained from it keep naming it.</summary>
[ValidateModel]
public class DeleteRoutineEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult
{
    private readonly IRoutineService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public DeleteRoutineEndpoint(IRoutineService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpDelete("api/training/routines/{id:guid}")]
    [EndpointDescription("Delete a routine.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult> HandleAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.DeleteAsync(user, id, cancellationToken));
    }
}
