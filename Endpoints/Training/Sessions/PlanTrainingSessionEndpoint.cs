using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Training.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Sessions;

/// <summary>Plans a session for a future date.</summary>
[ValidateModel]
public class PlanTrainingSessionEndpoint : EndpointBaseAsync
    .WithRequest<PlanSessionRequest>
    .WithActionResult<TrainingSessionView>
{
    private readonly ITrainingSessionService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public PlanTrainingSessionEndpoint(ITrainingSessionService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/training/sessions/planned")]
    [EndpointDescription("Plan a training session, with its prescribed sets.")]
    [ProducesResponseType<TrainingSessionView>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<TrainingSessionView>> HandleAsync([FromBody] PlanSessionRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.PlanAsync(user, request, cancellationToken),
            StatusCodes.Status201Created);
    }
}
