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
    public class GetAllChatConversationsEndpoint : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<List<ChatConversationDTO>>
    {
        private readonly IChatConversationService _chatConversationService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetAllChatConversationsEndpoint(IChatConversationService chatConversationService, UserManager<IdentityUser> userManager)
        {
            _chatConversationService = chatConversationService;
            _userManager = userManager;
        }

        [HttpGet("api/chat/get-all")]
        [EndpointDescription("Get all chat conversations for the current user.")]
        [ProducesResponseType<List<ChatConversationDTO>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<List<ChatConversationDTO>>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _chatConversationService.GetAllChatConversationsAsync(user));
        }
    }
}
