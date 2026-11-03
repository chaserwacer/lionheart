using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Programs;
using lionheart.Services.Training.Programs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Programs;

/// <summary>Retitles or re-dates a program without touching its lifecycle state.</summary>
[ValidateModel]
public class UpdateTrainingProgramEndpoint : EndpointBaseAsync
    .WithRequest<Addressed<UpdateProgramRequest>>
    .WithActionResult<TrainingProgramView>
{
    private readonly ITrainingProgramService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public UpdateTrainingProgramEndpoint(ITrainingProgramService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPatch("api/training/programs/{id:guid}")]
    [EndpointDescription("Update a training program's title, dates and tags.")]
    [ProducesResponseType<TrainingProgramView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<TrainingProgramView>> HandleAsync(Addressed<UpdateProgramRequest> request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.UpdateAsync(user, request.Id, request.Body, cancellationToken),
            StatusCodes.Status200OK);
    }
}
