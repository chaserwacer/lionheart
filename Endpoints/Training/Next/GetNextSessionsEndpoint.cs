using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.Next;
using lionheart.Services.Training.Next;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.Next;

/// <summary>What to train next as of a day: the next lift, the next cardio, and which comes first.</summary>
[ValidateModel]
public class GetNextSessionsEndpoint : EndpointBaseAsync
    .WithRequest<DateOnly>
    .WithActionResult<NextSessions>
{
    private readonly INextSessionsService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetNextSessionsEndpoint(INextSessionsService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/training/next/{day:datetime}")]
    [EndpointDescription("Get the next lift, the next cardio session and which to do first as of a day, each with its reasons.")]
    [ProducesResponseType<NextSessions>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<NextSessions>> HandleAsync(
        [FromRoute] DateOnly day, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.GetNextAsync(user, day, cancellationToken), StatusCodes.Status200OK);
    }
}
