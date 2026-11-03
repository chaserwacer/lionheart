using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Model.Habits;
using lionheart.Services.Habits;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Habits;

/// <summary>
/// How the athlete's days are shaped over a range — sitting, movement by hour, training times, sleep
/// timing — against the equal span before it.
/// </summary>
[ValidateModel]
public class GetHabitsReportEndpoint : EndpointBaseAsync
    .WithRequest<DayRange>
    .WithActionResult<HabitsReport>
{
    private readonly IHabitsReportService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetHabitsReportEndpoint(IHabitsReportService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/habits")]
    [EndpointDescription("Get the habits report for a range of days.")]
    [ProducesResponseType<HabitsReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<HabitsReport>> HandleAsync(
        [FromQuery] DayRange request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetReportAsync(user, request, cancellationToken),
            StatusCodes.Status200OK);
    }
}
