using Ardalis.ApiEndpoints;
using lionheart.Model.Training.Sessions;
using lionheart.Services.Training.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Sessions;

/// <summary>For every movement the athlete has trained, the sets of it from the last session that held it.</summary>
public class GetPreviousPerformancesEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<IReadOnlyList<PreviousPerformanceView>>
{
    private readonly IPreviousPerformanceService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetPreviousPerformancesEndpoint(IPreviousPerformanceService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/previous-performances")]
    [EndpointDescription("Get the previous performance on every movement: its sets from the most recent completed session holding it.")]
    [ProducesResponseType<IReadOnlyList<PreviousPerformanceView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<IReadOnlyList<PreviousPerformanceView>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetAllAsync(user, cancellationToken),
            StatusCodes.Status200OK);
    }
}
