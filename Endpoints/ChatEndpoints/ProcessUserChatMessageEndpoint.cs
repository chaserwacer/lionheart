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
    public class ProcessUserChatMessageEndpoint : EndpointBaseAsync
        .WithRequest<AddChatMessageRequest>
        .WithActionResult<LHChatMessageDTO>
    {
        private readonly IChatMessageService _chatMessageService;
        private readonly UserManager<IdentityUser> _userManager;

        public ProcessUserChatMessageEndpoint(IChatMessageService chatMessageService, UserManager<IdentityUser> userManager)
        {
            _chatMessageService = chatMessageService;
            _userManager = userManager;
        }

        [HttpPost("api/chat/message/process")]
        [EndpointDescription("Process a user chat message, generate AI response, and store both.")]
        [ProducesResponseType<LHChatMessageDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<LHChatMessageDTO>> HandleAsync([FromBody] AddChatMessageRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized("User is not recognized or no longer exists.");

            return this.ToActionResult(await _chatMessageService.ProcessUserChatMessageAsync(user, request));
        }
    }
}
