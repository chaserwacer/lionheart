using Ardalis.ApiEndpoints;
using Ardalis.Filters;
using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using lionheart.Model.DTOs;
using lionheart.Services.AI;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.AIEndpoints
{
    [ValidateModel]
    public class ChatEndpoint : EndpointBaseAsync
        .WithRequest<ChatRequest>
        .WithActionResult<ChatResponse>
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IChatService _chatService;
        
        public ChatEndpoint(
            UserManager<IdentityUser> userManager,
            IChatService chatService)
        {
            _userManager = userManager;
            _chatService = chatService;
        }

        [HttpPost("api/ai/chat")]
        [EndpointDescription("Chat with the LLM assistant about training data and get personalized responses")]
        [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<ChatResponse>> HandleAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            // Get the authenticated user
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Unauthorized("User is not recognized or no longer exists.");
            
            var result = await _chatService.ProcessChatMessageAsync(user, request);
            return this.ToActionResult(result);
        }
    }
}
