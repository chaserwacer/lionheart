using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Services.Training.Programs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Programs;

/// <summary>Deletes a program that no session still belongs to.</summary>
[ValidateModel]
public class DeleteTrainingProgramEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult
{
    private readonly ITrainingProgramService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public DeleteTrainingProgramEndpoint(ITrainingProgramService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpDelete("api/training/programs/{id:guid}")]
    [EndpointDescription("Delete a training program.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult> HandleAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.DeleteAsync(user, id, cancellationToken));
    }
}
