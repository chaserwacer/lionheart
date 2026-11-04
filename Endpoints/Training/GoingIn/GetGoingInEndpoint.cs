using Ardalis.ApiEndpoints;
using lionheart.Model.Training.GoingIn;
using lionheart.Services.Training.GoingIn;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.GoingIn;

/// <summary>How ready the athlete is for a session now: the going-in rating, its parts, and what to note.</summary>
public class GetGoingInEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<GoingInState>
{
    private readonly IGoingInService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetGoingInEndpoint(IGoingInService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/going-in")]
    [EndpointDescription("Get the going-in rating for a session now, with each part and what it rests on, the active injuries, today's plans and the muscles trained since yesterday.")]
    [ProducesResponseType<GoingInState>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<GoingInState>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.GetAsync(user, cancellationToken), StatusCodes.Status200OK);
    }
}
