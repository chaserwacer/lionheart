using Ardalis.ApiEndpoints;
using lionheart.Model.Chat.Memory;
using lionheart.Services.Chat.Memory;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ChatEndpoints.Memory;

/// <summary>What Lionheart remembers from the chat thread.</summary>
public class GetChatMemoriesEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<IReadOnlyList<MemoryView>>
{
    private readonly IChatMemoryService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetChatMemoriesEndpoint(IChatMemoryService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/chat/memories")]
    [EndpointDescription("Every current memory distilled from the chat thread — goals, injuries and preferences first, then newest first — each with the messages it came from and the versions it replaced.")]
    [ProducesResponseType<IReadOnlyList<MemoryView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<IReadOnlyList<MemoryView>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.GetCurrentAsync(user, cancellationToken));
    }
}
