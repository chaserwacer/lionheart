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
    public class CreateChatConversationEndpoint : EndpointBaseAsync
        .WithRequest<CreateChatConversationRequest>
        .WithActionResult<LHChatConversationDTO>
    {
        private readonly IChatConversationService _chatConversationService;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateChatConversationEndpoint(IChatConversationService chatConversationService, UserManager<IdentityUser> userManager)
        {
            _chatConversationService = chatConversationService;
            _userManager = userManager;
        }

        [HttpPost("api/chat/conversation/create")]
        [EndpointDescription("Create a new chat conversation.")]
        [ProducesResponseType<LHChatConversationDTO>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<LHChatConversationDTO>> HandleAsync([FromBody] CreateChatConversationRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized("User is not recognized or no longer exists.");

            return this.ToActionResult(await _chatConversationService.CreateChatConversationAsync(user, request));
        }
    }
}
