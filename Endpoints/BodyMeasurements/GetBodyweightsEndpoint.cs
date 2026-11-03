using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Model.BodyMeasurements;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.BodyMeasurements;

/// <summary>Every bodyweight measured in a range of days, oldest first.</summary>
[ValidateModel]
public class GetBodyweightsEndpoint : EndpointBaseAsync
    .WithRequest<DayRange>
    .WithActionResult<IReadOnlyList<BodyweightView>>
{
    private readonly IBodyweightService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetBodyweightsEndpoint(IBodyweightService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/bodyweight")]
    [EndpointDescription("Get the bodyweights measured in a range of days.")]
    [ProducesResponseType<IReadOnlyList<BodyweightView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<IReadOnlyList<BodyweightView>>> HandleAsync([FromQuery] DayRange request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetRangeAsync(user, request, cancellationToken),
            StatusCodes.Status200OK);
    }
}
