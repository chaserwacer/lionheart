using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Services.Oura;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Oura;

/// <summary>Every sleep period starting inside a window, with its stages, hypnogram and overnight heart-rate and HRV curves.</summary>
[ValidateModel]
public class GetOuraSleepEndpoint : EndpointBaseAsync
    .WithRequest<TimeWindow>
    .WithActionResult<IReadOnlyList<OuraEventView<SleepPeriodReading>>>
{
    private readonly IOuraReadService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetOuraSleepEndpoint(IOuraReadService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/oura/sleep")]
    [EndpointDescription("Get the Oura sleep periods falling inside a time window.")]
    [ProducesResponseType<IReadOnlyList<OuraEventView<SleepPeriodReading>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public override async Task<ActionResult<IReadOnlyList<OuraEventView<SleepPeriodReading>>>> HandleAsync(
        [FromQuery] TimeWindow request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetSleepAsync(user, request, cancellationToken),
            StatusCodes.Status200OK);
    }
}
