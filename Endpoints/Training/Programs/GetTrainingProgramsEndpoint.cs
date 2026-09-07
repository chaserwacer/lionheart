using Ardalis.ApiEndpoints;
using lionheart.Model.Training.Programs;
using lionheart.Services.Training.Programs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Programs;

/// <summary>Every program this athlete has, newest block first.</summary>
public class GetTrainingProgramsEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<IReadOnlyList<TrainingProgramView>>
{
    private readonly ITrainingProgramService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetTrainingProgramsEndpoint(ITrainingProgramService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/programs")]
    [EndpointDescription("Get every training program for the athlete.")]
    [ProducesResponseType<IReadOnlyList<TrainingProgramView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<IReadOnlyList<TrainingProgramView>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.GetAllAsync(user, cancellationToken));
    }
}
