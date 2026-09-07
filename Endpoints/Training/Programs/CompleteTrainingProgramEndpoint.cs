using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Programs;
using lionheart.Services.Training.Programs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Programs;

/// <summary>Active to Completed, which is the only state that carries a summary.</summary>
[ValidateModel]
public class CompleteTrainingProgramEndpoint : EndpointBaseAsync
    .WithRequest<Addressed<CompleteProgramRequest>>
    .WithActionResult<TrainingProgramView>
{
    private readonly ITrainingProgramService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public CompleteTrainingProgramEndpoint(ITrainingProgramService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/training/programs/{id:guid}/complete")]
    [EndpointDescription("Complete an active training program.")]
    [ProducesResponseType<TrainingProgramView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<TrainingProgramView>> HandleAsync(Addressed<CompleteProgramRequest> request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.CompleteAsync(user, request.Id, request.Body, cancellationToken),
            StatusCodes.Status200OK);
    }
}
