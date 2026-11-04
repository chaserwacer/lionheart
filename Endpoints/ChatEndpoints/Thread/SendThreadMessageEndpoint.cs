using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Chat;
using lionheart.Model.Chat.Thread;
using lionheart.Services.Chat.Thread;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ChatEndpoints.Thread;

/// <summary>One message to the continuing thread, answered in whichever session it belongs to.</summary>
[ValidateModel]
public class SendThreadMessageEndpoint : EndpointBaseAsync
    .WithRequest<SendThreadMessageRequest>
    .WithActionResult<ThreadSessionView>
{
    private readonly IChatThreadService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public SendThreadMessageEndpoint(IChatThreadService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/chat/thread/messages")]
    [EndpointDescription("Send one message to the continuing thread and wait for the answer. Responds with the whole session the exchange went into: a new one when the thread had sat idle past the session gap or grown full.")]
    [ProducesResponseType<ThreadSessionView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public override async Task<ActionResult<ThreadSessionView>> HandleAsync(
        [FromBody] SendThreadMessageRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.SendAsync(user, request, ExchangeObserver.Unwatched, cancellationToken));
    }
}
