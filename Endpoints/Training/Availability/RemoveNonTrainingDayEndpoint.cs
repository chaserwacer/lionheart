using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Services.Training.Availability;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Availability;

/// <summary>Removes a non-training day; a weekly one frees every day it covered.</summary>
[ValidateModel]
public class RemoveNonTrainingDayEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult
{
    private readonly INonTrainingDayService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public RemoveNonTrainingDayEndpoint(INonTrainingDayService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpDelete("api/training/non-training-days/{id:guid}")]
    [EndpointDescription("Remove a non-training day; a weekly one frees every day it covered.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult> HandleAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.RemoveAsync(user, id, cancellationToken));
    }
}
