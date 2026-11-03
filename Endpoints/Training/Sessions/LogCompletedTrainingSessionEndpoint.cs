using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Training.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Sessions;

/// <summary>Logs a session that already happened, in one call.</summary>
[ValidateModel]
public class LogCompletedTrainingSessionEndpoint : EndpointBaseAsync
    .WithRequest<LogCompletedSessionRequest>
    .WithActionResult<TrainingSessionView>
{
    private readonly ITrainingSessionService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public LogCompletedTrainingSessionEndpoint(ITrainingSessionService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/training/sessions/completed")]
    [EndpointDescription("Log a training session after the fact, with its performed sets.")]
    [ProducesResponseType<TrainingSessionView>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<TrainingSessionView>> HandleAsync([FromBody] LogCompletedSessionRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.LogCompletedAsync(user, request, cancellationToken),
            StatusCodes.Status201Created);
    }
}
