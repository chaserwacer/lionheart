using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Services.Oura;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Oura;

/// <summary>Every all-day heart-rate sample inside a window: the instant, the rate, and what the ring was doing.</summary>
[ValidateModel]
public class GetOuraHeartRateEndpoint : EndpointBaseAsync
    .WithRequest<TimeWindow>
    .WithActionResult<IReadOnlyList<HeartRateSample>>
{
    private readonly IOuraReadService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetOuraHeartRateEndpoint(IOuraReadService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/oura/heartrate")]
    [EndpointDescription("Get the all-day heart-rate samples inside a time window of at most 31 days.")]
    [ProducesResponseType<IReadOnlyList<HeartRateSample>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public override async Task<ActionResult<IReadOnlyList<HeartRateSample>>> HandleAsync(
        [FromQuery] TimeWindow request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetHeartRateAsync(user, request, cancellationToken),
            StatusCodes.Status200OK);
    }
}
