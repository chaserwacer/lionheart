using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;            // for ChatMessage
using Ardalis.Result.AspNetCore;          // for ToActionResult
using lionheart.Services;
using Model.McpServer;                    // for LionMcpPrompt, InstructionPromptSection

namespace lionheart.Controllers
{
    /// <summary>
    /// Controller for handling chat interactions with the Model Context Protocol (MCP).
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class MCPChatController : ControllerBase
    {
        private readonly ILogger<MCPChatController> _logger;
        private readonly IMCPClientService _chatService;
        private readonly UserManager<IdentityUser> _userManager;

        public MCPChatController(
            ILogger<MCPChatController> logger,
            IMCPClientService chatService,
            UserManager<IdentityUser> userManager)
        {
            _logger = logger;
            _chatService = chatService;
            _userManager = userManager;
        }

        [HttpPost(Name = "Chat")]
        public async Task<ActionResult<string>> Chat([FromBody] string userPrompt)
        {
            // 1) Get the ASP.NET Identity user
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Unauthorized("User is not recognized or no longer exists.");

            // 2) Build the MCP prompt around the raw user input
            var prompt = new LionMcpPrompt { User = user };
            var userSection = new InstructionPromptSection { Name = "User Prompt" };
            userSection.AddInstruction(userPrompt);
            prompt.Sections.Add(userSection);

            // 3) Convert to ChatMessage list and call the new overload
            var messages = prompt.ToChatMessage();
            var result = await _chatService.ChatAsync(user, messages);

            // 4) Return via ToActionResult
            return this.ToActionResult(result);
        }
         [HttpGet(Name = "PromptTest")]
        public async Task<ActionResult<string>> PromptTesty()
        {
            // 1) Get the ASP.NET Identity user
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Unauthorized("User is not recognized or no longer exists.");

            return this.ToActionResult(await _chatService.ChatAsync(user));
        }
    }
}
