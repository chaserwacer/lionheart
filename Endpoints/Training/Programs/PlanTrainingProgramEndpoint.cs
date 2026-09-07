using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Programs;
using lionheart.Services.Training.Programs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Programs;

/// <summary>A program is always born planned.</summary>
[ValidateModel]
public class PlanTrainingProgramEndpoint : EndpointBaseAsync
    .WithRequest<PlanProgramRequest>
    .WithActionResult<TrainingProgramView>
{
    private readonly ITrainingProgramService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public PlanTrainingProgramEndpoint(ITrainingProgramService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/training/programs")]
    [EndpointDescription("Plan a training program.")]
    [ProducesResponseType<TrainingProgramView>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public override async Task<ActionResult<TrainingProgramView>> HandleAsync([FromBody] PlanProgramRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.PlanAsync(user, request, cancellationToken),
            StatusCodes.Status201Created);
    }
}
