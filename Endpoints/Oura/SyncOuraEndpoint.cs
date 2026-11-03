using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Services.Oura;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Oura;

/// <summary>
/// Pulls every supported Oura collection for a range of days and stores what came back.
/// </summary>
/// <remarks>
/// Answers with the run's tallies rather than 204, because a sync is the one write here whose
/// outcome the caller cannot see any other way: how many documents each collection returned, how
/// many were stored, and the ones Oura sent that this application could not read.
/// </remarks>
[ValidateModel]
public class SyncOuraEndpoint : EndpointBaseAsync
    .WithRequest<DayRange>
    .WithActionResult<OuraSyncReport>
{
    private readonly IOuraSyncService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public SyncOuraEndpoint(IOuraSyncService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/oura/sync")]
    [EndpointDescription("Synchronize every supported Oura collection for a range of days.")]
    [ProducesResponseType<OuraSyncReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public override async Task<ActionResult<OuraSyncReport>> HandleAsync(
        [FromBody] DayRange request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.SyncAsync(user, request, cancellationToken),
            StatusCodes.Status200OK);
    }
}
