using Ardalis.ApiEndpoints;
using Ardalis.Filters;
using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using lionheart.Model.DTOs;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ChatEndpoints
{
    [ValidateModel]
    public class DeleteChatConversationEndpoint : EndpointBaseAsync
        .WithRequest<DeleteChatConversationRequest>
        .WithActionResult
    {
        private readonly IChatConversationService _chatConversationService;
        private readonly UserManager<IdentityUser> _userManager;

        public DeleteChatConversationEndpoint(IChatConversationService chatConversationService, UserManager<IdentityUser> userManager)
        {
            _chatConversationService = chatConversationService;
            _userManager = userManager;
        }

        [HttpDelete("api/chat")]
        [EndpointDescription("Delete a chat conversation.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult> HandleAsync(DeleteChatConversationRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _chatConversationService.DeleteChatConversationAsync(user, request));
        }
    }
}
