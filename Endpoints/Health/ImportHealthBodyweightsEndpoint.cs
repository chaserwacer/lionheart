using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Health.Measurements;
using lionheart.Services.Health.Measurements;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Health;

/// <summary>Fills the days with no bodyweight from the weigh-ins Health holds.</summary>
[ValidateModel]
public class ImportHealthBodyweightsEndpoint : EndpointBaseAsync
    .WithRequest<HealthBodyweightsImport>
    .WithActionResult<HealthBodyweightImportReport>
{
    private readonly IHealthBodyweightImportService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public ImportHealthBodyweightsEndpoint(IHealthBodyweightImportService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/health/bodyweights")]
    [EndpointDescription("Fill the days with no bodyweight from Apple Health's weigh-ins: each day's first, in the athlete's unit. A day already measured is kept. Answers how many days were added and how many weighted-bodyweight sets now read a different bodyweight.")]
    [ProducesResponseType<HealthBodyweightImportReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<HealthBodyweightImportReport>> HandleAsync(
        [FromBody] HealthBodyweightsImport request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.ImportAsync(user, request, cancellationToken));
    }
}
