using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Availability;
using lionheart.Services.Training.Availability;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Availability;

/// <summary>Each day of a range as training availability reads it: open, planned or non-training.</summary>
[ValidateModel]
public class GetAvailabilityEndpoint : EndpointBaseAsync
    .WithRequest<DayRange>
    .WithActionResult<AvailabilityCalendar>
{
    private readonly INonTrainingDayService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetAvailabilityEndpoint(INonTrainingDayService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/availability")]
    [EndpointDescription("Get each day of a range, at most 371, as open, planned or non-training.")]
    [ProducesResponseType<AvailabilityCalendar>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<AvailabilityCalendar>> HandleAsync(
        [FromQuery] DayRange request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.GetAvailabilityAsync(user, request, cancellationToken), StatusCodes.Status200OK);
    }
}
