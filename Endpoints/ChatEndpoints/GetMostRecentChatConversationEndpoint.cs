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
    public class GetMostRecentChatConversationEndpoint : EndpointBaseAsync
.WithoutRequest
        .WithActionResult<LHChatConversationDTO>
    {
        private readonly IChatConversationService _chatConversationService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetMostRecentChatConversationEndpoint(IChatConversationService chatConversationService, UserManager<IdentityUser> userManager)
        {
            _chatConversationService = chatConversationService;
            _userManager = userManager;
        }

        [HttpGet("api/chat/conversation/most-recent")]
        [EndpointDescription("Get the most recent chat conversation for the current user.")]
        [ProducesResponseType<LHChatConversationDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<LHChatConversationDTO>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized();

            return this.ToActionResult(await _chatConversationService.GetMostRecentChatConversationAsync(user));


        }
    }
}
