using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Habits;
using lionheart.Services.Habits;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Habits;

/// <summary>One Oura activity day as a timeline: its classified runs, long sittings, sleep and training.</summary>
[ValidateModel]
public class GetHabitsDayEndpoint : EndpointBaseAsync
    .WithRequest<DateOnly>
    .WithActionResult<HabitsDayDetail>
{
    private readonly IHabitsReportService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetHabitsDayEndpoint(IHabitsReportService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/habits/days/{day:datetime}")]
    [EndpointDescription("Get one day's movement timeline.")]
    [ProducesResponseType<HabitsDayDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<HabitsDayDetail>> HandleAsync(
        [FromRoute] DateOnly day, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetDayAsync(user, day, cancellationToken),
            StatusCodes.Status200OK);
    }
}
