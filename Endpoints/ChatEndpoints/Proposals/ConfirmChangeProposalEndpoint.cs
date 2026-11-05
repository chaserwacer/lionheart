using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Chat.Changes;
using lionheart.Services.Chat.Changes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ChatEndpoints.Proposals;

/// <summary>The athlete confirms a change the chat proposed: the only route that writes one.</summary>
[ValidateModel]
public class ConfirmChangeProposalEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<ChangeProposalView>
{
    private readonly IChangeProposalService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public ConfirmChangeProposalEndpoint(IChangeProposalService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/chat/proposals/{id:guid}/confirm")]
    [EndpointDescription("Confirm a change the chat proposed. Writes it through the service its page uses and answers with the proposal settled: confirmed, or refused with that service's reason when the write could not be made. 409 when it was already answered.")]
    [ProducesResponseType<ChangeProposalView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<ChangeProposalView>> HandleAsync([FromRoute] Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.ConfirmAsync(user, id, cancellationToken));
    }
}
