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
    public class GetChatConversationEndpoint : EndpointBaseAsync
        .WithRequest<Guid>
        .WithActionResult<ChatConversationDTO>
    {
        private readonly IChatConversationService _chatConversationService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetChatConversationEndpoint(IChatConversationService chatConversationService, UserManager<IdentityUser> userManager)
        {
            _chatConversationService = chatConversationService;
            _userManager = userManager;
        }

        [HttpGet("api/chat/{conversationId}")]
        [EndpointDescription("Get a specific chat conversation by ID.")]
        [ProducesResponseType<ChatConversationDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<ChatConversationDTO>> HandleAsync([FromRoute] Guid conversationId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _chatConversationService.GetChatConversationAsync(user, conversationId));
        }
    }
}
