using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Balance;
using lionheart.Services.Training.Balance;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Balance;

/// <summary>Hard sets by family, pattern and muscle group over the seven days ending on a day, and what is up next.</summary>
[ValidateModel]
public class GetTrainingVolumeEndpoint : EndpointBaseAsync
    .WithRequest<DateOnly>
    .WithActionResult<TrainingVolume>
{
    private readonly ITrainingVolumeService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetTrainingVolumeEndpoint(ITrainingVolumeService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/volume/{day:datetime}")]
    [EndpointDescription("Get the hard sets of the seven days ending on a day, by family, pattern and muscle group, with what is up next.")]
    [ProducesResponseType<TrainingVolume>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<TrainingVolume>> HandleAsync(
        [FromRoute] DateOnly day, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.GetVolumeAsync(user, day, cancellationToken), StatusCodes.Status200OK);
    }
}
