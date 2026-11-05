using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Availability;
using lionheart.Services.Training.Availability;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Availability;

/// <summary>Marks a day, or its weekday every week, non-training.</summary>
[ValidateModel]
public class MarkNonTrainingDayEndpoint : EndpointBaseAsync
    .WithRequest<MarkNonTrainingDayRequest>
    .WithActionResult<NonTrainingDayView>
{
    private readonly INonTrainingDayService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public MarkNonTrainingDayEndpoint(INonTrainingDayService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/training/non-training-days")]
    [EndpointDescription("Mark a day non-training, once or every week from it, optionally until a day, with an optional note.")]
    [ProducesResponseType<NonTrainingDayView>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<NonTrainingDayView>> HandleAsync(
        [FromBody] MarkNonTrainingDayRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.MarkAsync(user, request, cancellationToken), StatusCodes.Status201Created);
    }
}
