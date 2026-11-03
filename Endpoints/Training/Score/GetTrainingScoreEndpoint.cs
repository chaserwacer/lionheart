using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Score;
using lionheart.Services.Training.Score;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Score;

/// <summary>Lionheart's training score for the seven days ending on a day, part by part.</summary>
[ValidateModel]
public class GetTrainingScoreEndpoint : EndpointBaseAsync
    .WithRequest<DateOnly>
    .WithActionResult<TrainingScore>
{
    private readonly ITrainingScoreService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetTrainingScoreEndpoint(ITrainingScoreService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/score/{day:datetime}")]
    [EndpointDescription("Get the training score of the seven days ending on a day, with the measurements behind each part.")]
    [ProducesResponseType<TrainingScore>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<TrainingScore>> HandleAsync(
        [FromRoute] DateOnly day, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.GetScoreAsync(user, day, cancellationToken), StatusCodes.Status200OK);
    }
}
