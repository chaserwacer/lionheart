using Ardalis.ApiEndpoints;
using Ardalis.Filters;
using Ardalis.Result.AspNetCore;
using lionheart.Services.Chat;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ChatEndpoints
{
    [ValidateModel]
    public class DeleteChatConversationEndpoint : EndpointBaseAsync
        .WithRequest<Guid>
        .WithActionResult
    {
        private readonly IChatConversationService _chatConversationService;
        private readonly UserManager<IdentityUser> _userManager;

        public DeleteChatConversationEndpoint(IChatConversationService chatConversationService, UserManager<IdentityUser> userManager)
        {
            _chatConversationService = chatConversationService;
            _userManager = userManager;
        }

        [HttpDelete("api/chat/conversation/{conversationId}")]
        [EndpointDescription("Delete a chat conversation by ID.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult> HandleAsync([FromRoute] Guid conversationId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized("User is not recognized or no longer exists.");

            var result = await _chatConversationService.DeleteChatConversationAsync(user, conversationId);
            return this.ToActionResult(result);
        }
    }
}
