using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Model.DTOs;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.PromptEndpoints
{
    [ValidateModel]
    public class GenerateOpenAIPromptEndpoint : EndpointBaseAsync
        .WithRequest<GeneratePromptRequest>
        .WithActionResult<string>
    {
        private readonly IPromptService _promptService;
        private readonly UserManager<IdentityUser> _userManager;

        public GenerateOpenAIPromptEndpoint(IPromptService promptService, UserManager<IdentityUser> userManager)
        {
            _promptService = promptService;
            _userManager = userManager;
        }

        [HttpPost("api/openai/prompt")]
        [EndpointDescription("Generates a system prompt using OpenAI-specific logic and user-defined inputs.")]
        [ProducesResponseType<string>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<string>> HandleAsync(
            [FromBody] GeneratePromptRequest request,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Unauthorized("User is not recognized.");

            var result = await _promptService.GeneratePromptAsync(user, request);
            return this.ToActionResult(result);
        }
    }
}
