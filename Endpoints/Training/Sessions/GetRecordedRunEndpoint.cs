using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Health;
using lionheart.Services.Health;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Sessions;

/// <summary>A session imported from Apple Health, read as a run: route, splits and heart rate.</summary>
[ValidateModel]
public class GetRecordedRunEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<RecordedRunView>
{
    private readonly IRecordedRunService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetRecordedRunEndpoint(IRecordedRunService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/sessions/{id:guid}/recording")]
    [EndpointDescription("Get a run imported from Apple Health: its route, its splits per kilometre and per mile, and its heart rate.")]
    [ProducesResponseType<RecordedRunView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<RecordedRunView>> HandleAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetAsync(user, id, cancellationToken),
            StatusCodes.Status200OK);
    }
}
