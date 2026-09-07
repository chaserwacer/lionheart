using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Training.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Sessions;

/// <summary>Notes are commentary, editable in every lifecycle state.</summary>
[ValidateModel]
public class UpdateTrainingSessionNotesEndpoint : EndpointBaseAsync
    .WithRequest<Addressed<SessionNotesRequest>>
    .WithActionResult<TrainingSessionView>
{
    private readonly ITrainingSessionService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public UpdateTrainingSessionNotesEndpoint(ITrainingSessionService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPatch("api/training/sessions/{id:guid}/notes")]
    [EndpointDescription("Update a training session's notes.")]
    [ProducesResponseType<TrainingSessionView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<TrainingSessionView>> HandleAsync(Addressed<SessionNotesRequest> request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.UpdateNotesAsync(user, request.Id, request.Body, cancellationToken),
            StatusCodes.Status200OK);
    }
}
