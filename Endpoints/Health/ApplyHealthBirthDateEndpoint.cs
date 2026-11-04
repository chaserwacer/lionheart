using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Health;
using lionheart.Services.Health.Measurements;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Health;

/// <summary>Keeps the profile's age current from the date of birth Health holds.</summary>
[ValidateModel]
public class ApplyHealthBirthDateEndpoint : EndpointBaseAsync
    .WithRequest<HealthBirthDate>
    .WithActionResult<ProfileAgeView>
{
    private readonly IHealthBirthDateService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public ApplyHealthBirthDateEndpoint(IHealthBirthDateService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPut("api/health/birth-date")]
    [EndpointDescription("Set the profile's age from Apple Health's date of birth when it is unset or stale. Answers the age and whether it changed.")]
    [ProducesResponseType<ProfileAgeView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<ProfileAgeView>> HandleAsync(
        [FromBody] HealthBirthDate request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.ApplyAsync(user, request, cancellationToken));
    }
}
