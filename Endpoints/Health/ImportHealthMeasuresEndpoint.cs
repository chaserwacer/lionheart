using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Health.Measurements;
using lionheart.Services.Health.Measurements;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Health;

/// <summary>Keeps days of the figures Apple's devices measured; restates a day already held.</summary>
[ValidateModel]
public class ImportHealthMeasuresEndpoint : EndpointBaseAsync
    .WithRequest<HealthMeasuresImport>
    .WithActionResult<HealthMeasuresImportReport>
{
    private readonly IHealthMeasureService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public ImportHealthMeasuresEndpoint(IHealthMeasureService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/health/measurements")]
    [EndpointDescription("Keep days of Apple Health figures measured by the athlete's Apple Watch or iPhone: VO2max, resting heart rate, HRV (SDNN), respiratory rate, walking heart rate, and gait. A day already held is restated. Answers how many days were new, restated, unchanged or unreadable.")]
    [ProducesResponseType<HealthMeasuresImportReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<HealthMeasuresImportReport>> HandleAsync(
        [FromBody] HealthMeasuresImport request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.ImportAsync(user, request, cancellationToken));
    }
}
