using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Model.Cardio;
using lionheart.Services.Cardio;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Cardio;

/// <summary>
/// The athlete's cardio over a range of days: sessions with their heart-rate zones, weekly totals,
/// Oura's fitness estimates, and the aerobic-base comparison.
/// </summary>
[ValidateModel]
public class GetCardioReportEndpoint : EndpointBaseAsync
    .WithRequest<DayRange>
    .WithActionResult<CardioReport>
{
    private readonly ICardioReportService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetCardioReportEndpoint(ICardioReportService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/cardio")]
    [EndpointDescription("Get the cardio report for a range of days.")]
    [ProducesResponseType<CardioReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<CardioReport>> HandleAsync(
        [FromQuery] DayRange request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetReportAsync(user, request, cancellationToken),
            StatusCodes.Status200OK);
    }
}
