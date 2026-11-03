using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Training.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Sessions;

/// <summary>Active to Completed.</summary>
[ValidateModel]
public class CompleteTrainingSessionEndpoint : EndpointBaseAsync
    .WithRequest<Addressed<CompleteSessionRequest>>
    .WithActionResult<TrainingSessionView>
{
    private readonly ITrainingSessionService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public CompleteTrainingSessionEndpoint(ITrainingSessionService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/training/sessions/{id:guid}/complete")]
    [EndpointDescription("Complete an active training session.")]
    [ProducesResponseType<TrainingSessionView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<TrainingSessionView>> HandleAsync(Addressed<CompleteSessionRequest> request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.CompleteAsync(user, request.Id, request.Body, cancellationToken),
            StatusCodes.Status200OK);
    }
}
