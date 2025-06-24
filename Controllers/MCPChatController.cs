using Microsoft.AspNetCore.Mvc;
using lionheart.Services;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Ardalis.Result.AspNetCore;
namespace lionheart.Controllers
{
    /// <summary>
    /// Controller for habdling chat interactions with the Model Context Protocol (MCP).
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    public class MCPChatController : ControllerBase
    {
        private readonly ILogger<MCPChatController> _logger;
        private readonly IMCPClientService _chatService;
        private readonly UserManager<IdentityUser> _userManager;
        public MCPChatController(ILogger<MCPChatController> logger, IMCPClientService chatService, UserManager<IdentityUser> userManager)
        {
            _logger = logger;
            _chatService = chatService;
            _userManager = userManager;
        }

        [HttpPost(Name = "Chat")]
        public async Task<ActionResult<string>> Chat([FromBody] string userPrompt)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }
            return this.ToActionResult(await _chatService.ChatAsync(user));


        }
    }
}