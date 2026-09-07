using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Training.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Sessions;

/// <summary>Every session in a window, each lifecycle state matched on its own time-truth.</summary>
[ValidateModel]
public class GetTrainingSessionsEndpoint : EndpointBaseAsync
    .WithRequest<TimeWindow>
    .WithActionResult<IReadOnlyList<TrainingSessionView>>
{
    private readonly ITrainingSessionService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetTrainingSessionsEndpoint(ITrainingSessionService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/sessions")]
    [EndpointDescription("Get the training sessions falling inside a time window.")]
    [ProducesResponseType<IReadOnlyList<TrainingSessionView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public override async Task<ActionResult<IReadOnlyList<TrainingSessionView>>> HandleAsync([FromQuery] TimeWindow request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetWindowAsync(user, request, cancellationToken),
            StatusCodes.Status200OK);
    }
}
