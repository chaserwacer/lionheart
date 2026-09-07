using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Programs;
using lionheart.Services.Training.Programs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Programs;

/// <summary>One program, in whichever lifecycle state it is in.</summary>
[ValidateModel]
public class GetTrainingProgramEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<TrainingProgramView>
{
    private readonly ITrainingProgramService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetTrainingProgramEndpoint(ITrainingProgramService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/programs/{id:guid}")]
    [EndpointDescription("Get a training program by id.")]
    [ProducesResponseType<TrainingProgramView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<TrainingProgramView>> HandleAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetAsync(user, id, cancellationToken),
            StatusCodes.Status200OK);
    }
}
