using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Services.Chat.Memory;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ChatEndpoints.Memory;

/// <summary>Forgets a memory, every version of it.</summary>
[ValidateModel]
public class DeleteChatMemoryEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult
{
    private readonly IChatMemoryService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public DeleteChatMemoryEndpoint(IChatMemoryService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpDelete("api/chat/memories/{id:guid}")]
    [EndpointDescription("Forget a memory: it and every earlier version of it, so it reaches no session again.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult> HandleAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.DeleteAsync(user, id, cancellationToken));
    }
}
