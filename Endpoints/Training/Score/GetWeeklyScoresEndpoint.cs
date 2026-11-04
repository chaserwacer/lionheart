using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Model.Training.Score;
using lionheart.Services.Training.Score;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Score;

/// <summary>The training score of every Monday-to-Sunday week whose Sunday falls in a range.</summary>
[ValidateModel]
public class GetWeeklyScoresEndpoint : EndpointBaseAsync
    .WithRequest<DayRange>
    .WithActionResult<IReadOnlyList<WeeklyScore>>
{
    private readonly ITrainingScoreService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetWeeklyScoresEndpoint(ITrainingScoreService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/score/weeks")]
    [EndpointDescription("Get the training score of each week ending on a Sunday in the range, at most 48.")]
    [ProducesResponseType<IReadOnlyList<WeeklyScore>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<IReadOnlyList<WeeklyScore>>> HandleAsync(
        [FromQuery] DayRange request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.GetWeeksAsync(user, request, cancellationToken), StatusCodes.Status200OK);
    }
}
