using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Model.BodyMeasurements;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.BodyMeasurements;

/// <summary>Logs the athlete's bodyweight for a day, replacing whatever that day already held.</summary>
[ValidateModel]
public class LogBodyweightEndpoint : EndpointBaseAsync
    .WithRequest<Dated<BodyweightRequest>>
    .WithActionResult<BodyweightView>
{
    private readonly IBodyweightService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public LogBodyweightEndpoint(IBodyweightService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPut("api/bodyweight/{day:datetime}")]
    [EndpointDescription("Log the bodyweight measured on a day.")]
    [ProducesResponseType<BodyweightView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<BodyweightView>> HandleAsync(Dated<BodyweightRequest> request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.LogAsync(user, request.Day, request.Body, cancellationToken),
            StatusCodes.Status200OK);
    }
}
