using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.AspNetCore;
using System.Text;
using Microsoft.AspNetCore.Identity;
namespace McpClient.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class MCPChatController : ControllerBase
    {
        private readonly ILogger<MCPChatController> _logger;
        private readonly IChatClient _chatClient;
        private readonly UserManager<IdentityUser> _userManager;
        public MCPChatController(ILogger<MCPChatController> logger, IChatClient chatClient, UserManager<IdentityUser> userManager)
        {
            _logger = logger;
            _chatClient = chatClient;
            _userManager = userManager;
        }
        [HttpPost(Name = "Chat")]
        public async Task<ActionResult<string>> Chat([FromBody] string message)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            // Create MCP client connecting to our MCP server
            var mcpClient = await McpClientFactory.CreateAsync(
                new SseClientTransport(
                    new SseClientTransportOptions
                    {
                        Endpoint = new Uri("http://localhost:7025/sse")

                    }
                )
            );
            // Get available tools from the MCP server
            var tools = await mcpClient.ListToolsAsync();
            // Set up the chat messages
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, $"You are a helpful assistant, please utilize tools and resources available. Todays date is {DateTime.Now}"),
                new ChatMessage(ChatRole.User, $"Here is the userID for the current user: {user.Id}"),
                new(ChatRole.User, message)
            };
            // Get streaming response and collect updates
            List<ChatResponseUpdate> updates = [];
            StringBuilder result = new StringBuilder();

            await foreach (var update in _chatClient.GetStreamingResponseAsync(
                messages,
                new() { Tools = [.. tools] }
            ))
            {
                result.Append(update);
                updates.Add(update);
            }
            // Add the assistant's responses to the message history
            messages.AddMessages(updates);
            return Ok(result.ToString());
        }
        
      
    }
}