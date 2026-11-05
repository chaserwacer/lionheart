using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Chat.Changes;
using lionheart.Services.Chat.Changes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ChatEndpoints.Proposals;

/// <summary>The athlete declines a change the chat proposed; nothing is written.</summary>
[ValidateModel]
public class DeclineChangeProposalEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<ChangeProposalView>
{
    private readonly IChangeProposalService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public DeclineChangeProposalEndpoint(IChangeProposalService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/chat/proposals/{id:guid}/decline")]
    [EndpointDescription("Decline a change the chat proposed. Nothing is written; the proposal is settled as declined. 409 when it was already answered.")]
    [ProducesResponseType<ChangeProposalView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<ChangeProposalView>> HandleAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.DeclineAsync(user, id, cancellationToken));
    }
}
