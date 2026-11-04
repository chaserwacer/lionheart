using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Focus;
using lionheart.Services.Training.Focus;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Focus;

/// <summary>Each family's priority lift and the week's cardio, over the seven days ending on a day.</summary>
[ValidateModel]
public class GetPriorityReportEndpoint : EndpointBaseAsync
    .WithRequest<DateOnly>
    .WithActionResult<PriorityReport>
{
    private readonly IPriorityReportService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetPriorityReportEndpoint(IPriorityReportService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/priorities/{day:datetime}")]
    [EndpointDescription("Get each family's priority lift, its progress and frequency, and the week's cardio against its targets.")]
    [ProducesResponseType<PriorityReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<PriorityReport>> HandleAsync(
        [FromRoute] DateOnly day, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.GetReportAsync(user, day, cancellationToken), StatusCodes.Status200OK);
    }
}
