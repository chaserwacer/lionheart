using Ardalis.ApiEndpoints;
using Ardalis.Filters;
using Ardalis.Result.AspNetCore;
using lionheart.Model.Chat;
using lionheart.Services.Chat;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ChatEndpoints
{
    [ValidateModel]
    public class GetAllChatConversationsEndpoint : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<List<LHChatConversationDTO>>
    {
        private readonly IChatConversationService _chatConversationService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetAllChatConversationsEndpoint(IChatConversationService chatConversationService, UserManager<IdentityUser> userManager)
        {
            _chatConversationService = chatConversationService;
            _userManager = userManager;
        }

        [HttpGet("api/chat/conversations")]
        [EndpointDescription("Get all chat conversations for the current user.")]
        [ProducesResponseType<List<LHChatConversationDTO>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<List<LHChatConversationDTO>>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized();

            return this.ToActionResult(await _chatConversationService.GetAllChatConversationsAsync(user));
        }
    }
}
