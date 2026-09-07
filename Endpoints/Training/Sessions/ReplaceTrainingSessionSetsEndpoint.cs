using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Training.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Sessions;

/// <summary>Replaces an active session's whole ordered set list.</summary>
[ValidateModel]
public class ReplaceTrainingSessionSetsEndpoint : EndpointBaseAsync
    .WithRequest<Addressed<ReplaceSetsRequest>>
    .WithActionResult<TrainingSessionView>
{
    private readonly ITrainingSessionService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public ReplaceTrainingSessionSetsEndpoint(ITrainingSessionService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPut("api/training/sessions/{id:guid}/sets")]
    [EndpointDescription("Replace every set on an active training session.")]
    [ProducesResponseType<TrainingSessionView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<TrainingSessionView>> HandleAsync(Addressed<ReplaceSetsRequest> request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.ReplaceSetsAsync(user, request.Id, request.Body, cancellationToken),
            StatusCodes.Status200OK);
    }
}
