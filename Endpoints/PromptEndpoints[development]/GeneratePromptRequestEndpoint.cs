using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.Oura;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.OuraEndpoints
{
    [ValidateModel]
    public class GeneratePromptRequestEndpoint : EndpointBaseAsync
        .WithRequest<GeneratePromptRequest>
        .WithActionResult<string>
    {
        private readonly IPromptService _promptService;
        private readonly UserManager<IdentityUser> _userManager;

        public GeneratePromptRequestEndpoint(IPromptService promptService, UserManager<IdentityUser> userManager)
        {
            _promptService = promptService;
            _userManager = userManager;
        }

        [HttpGet("api/prompts/generate")]
        [EndpointDescription("Get a prompt based on request parameters.")]
        [ProducesResponseType<string>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<string>> HandleAsync([FromQuery] GeneratePromptRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _promptService.GeneratePromptAsync(user, request));
        }
    }
}