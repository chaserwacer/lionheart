using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Chat.Thread;
using lionheart.Services.Chat.Thread;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ChatEndpoints.Thread;

/// <summary>A stretch of the athlete's continuing chat thread: the newest sessions, or the ones before an instant.</summary>
[ValidateModel]
public class GetChatThreadEndpoint : EndpointBaseAsync
    .WithRequest<ChatThreadQuery>
    .WithActionResult<ChatThreadPage>
{
    private readonly IChatThreadService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetChatThreadEndpoint(IChatThreadService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/chat/thread")]
    [EndpointDescription("The newest sessions of the continuing chat thread, oldest first; with before=, the sessions that opened before that instant.")]
    [ProducesResponseType<ChatThreadPage>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<ChatThreadPage>> HandleAsync(
        [FromQuery] ChatThreadQuery request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.ReadAsync(user, request.ToPage(), cancellationToken));
    }
}
