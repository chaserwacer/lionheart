using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Chat.Memory;
using lionheart.Services.Chat.Memory;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ChatEndpoints.Memory;

/// <summary>The athlete's correction of a memory. The version it replaces is kept as history.</summary>
[ValidateModel]
public class CorrectChatMemoryEndpoint : EndpointBaseAsync
    .WithRequest<Addressed<CorrectMemoryRequest>>
    .WithActionResult<MemoryView>
{
    private readonly IChatMemoryService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public CorrectChatMemoryEndpoint(IChatMemoryService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPut("api/chat/memories/{id:guid}")]
    [EndpointDescription("Replace what a current memory says, and its kind, with the athlete's own wording. Answers with the new version; the old one stays as its history. A memory already replaced is a conflict.")]
    [ProducesResponseType<MemoryView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<MemoryView>> HandleAsync(
        Addressed<CorrectMemoryRequest> request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.CorrectAsync(user, request.Id, request.Body, cancellationToken));
    }
}
