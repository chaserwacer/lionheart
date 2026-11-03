using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Training.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Sessions;

/// <summary>One session, in whichever lifecycle state it is in.</summary>
[ValidateModel]
public class GetTrainingSessionEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<TrainingSessionView>
{
    private readonly ITrainingSessionService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetTrainingSessionEndpoint(ITrainingSessionService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/sessions/{id:guid}")]
    [EndpointDescription("Get a training session by id.")]
    [ProducesResponseType<TrainingSessionView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<TrainingSessionView>> HandleAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetAsync(user, id, cancellationToken),
            StatusCodes.Status200OK);
    }
}
