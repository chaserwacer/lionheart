using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Chat.Thread;
using lionheart.Services.Chat.Thread;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace lionheart.Endpoints.ChatEndpoints.Thread;

/// <summary>
/// One message to the continuing thread, answered as it is written. The same exchange as
/// <see cref="SendThreadMessageEndpoint"/>, told as server-sent events; see <see cref="ThreadEventStream"/>.
/// </summary>
[ValidateModel]
public class StreamThreadMessageEndpoint(
    IChatThreadService service,
    UserManager<IdentityUser> userManager,
    IOptions<JsonOptions> json,
    IHostApplicationLifetime lifetime) : EndpointBaseAsync
    .WithRequest<SendThreadMessageRequest>
    .WithoutResult
{
    private readonly IChatThreadService _service = service;
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly JsonOptions _json = json.Value;
    private readonly IHostApplicationLifetime _lifetime = lifetime;

    [HttpPost("api/chat/thread/messages/stream")]
    [EndpointDescription("Send one message to the continuing thread and follow the answer as it is written, as server-sent events: delta { text } for each piece of the answer, tools { tools } when a round of tools is run (withdrawing any text sent so far), then done with the ThreadSessionView the exchange was stored in, or failed with the problem document. The exchange finishes and is stored even if the connection drops.")]
    [Produces(ThreadEventStream.ContentType)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task HandleAsync(
        [FromBody] SendThreadMessageRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await ThreadEventStream.RunAsync(
            (observer, stopping) => _service.SendAsync(user, request, observer, stopping),
            _json.JsonSerializerOptions,
            Response,
            connection: cancellationToken,
            stopping: _lifetime.ApplicationStopping);
    }
}
