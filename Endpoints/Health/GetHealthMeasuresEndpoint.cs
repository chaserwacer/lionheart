using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Model.Health.Measurements;
using lionheart.Services.Health.Measurements;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Health;

/// <summary>The Apple Health figures kept for a range of days, one series per figure and device.</summary>
[ValidateModel]
public class GetHealthMeasuresEndpoint : EndpointBaseAsync
    .WithRequest<DayRange>
    .WithActionResult<IReadOnlyList<HealthMeasureSeries>>
{
    private readonly IHealthMeasureService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetHealthMeasuresEndpoint(IHealthMeasureService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/health/measurements")]
    [EndpointDescription("Get the Apple Health figures kept for a range of days: one series per figure and measuring device, each day's mean, lowest, highest and number of readings. HRV is SDNN and not comparable with Oura's RMSSD.")]
    [ProducesResponseType<IReadOnlyList<HealthMeasureSeries>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<IReadOnlyList<HealthMeasureSeries>>> HandleAsync(
        [FromQuery] DayRange request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.GetRangeAsync(user, request, cancellationToken));
    }
}
