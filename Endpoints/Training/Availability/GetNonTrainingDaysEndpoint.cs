using Ardalis.ApiEndpoints;
using lionheart.Model.Training.Availability;
using lionheart.Services.Training.Availability;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Availability;

/// <summary>Every non-training day the athlete has marked.</summary>
public class GetNonTrainingDaysEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<IReadOnlyList<NonTrainingDayView>>
{
    private readonly INonTrainingDayService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetNonTrainingDaysEndpoint(INonTrainingDayService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/non-training-days")]
    [EndpointDescription("Every non-training day the athlete has marked, one-off or weekly, the latest-starting first.")]
    [ProducesResponseType<IReadOnlyList<NonTrainingDayView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<IReadOnlyList<NonTrainingDayView>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.GetAllAsync(user, cancellationToken));
    }
}
