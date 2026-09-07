using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Programs;
using lionheart.Services.Training.Programs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Programs;

/// <summary>Planned to Active.</summary>
[ValidateModel]
public class ActivateTrainingProgramEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<TrainingProgramView>
{
    private readonly ITrainingProgramService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public ActivateTrainingProgramEndpoint(ITrainingProgramService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/training/programs/{id:guid}/activate")]
    [EndpointDescription("Activate a planned training program.")]
    [ProducesResponseType<TrainingProgramView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<TrainingProgramView>> HandleAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.ActivateAsync(user, id, cancellationToken),
            StatusCodes.Status200OK);
    }
}
