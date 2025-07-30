using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services.AI;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.DTOs;

namespace lionheart.Endpoints.AIEndpoints
{
    [ValidateModel]
    public class AnalyzeUserStateEndpoint : EndpointBaseAsync
        .WithRequest<DateOnly>
        .WithActionResult<string>
    {
        private readonly IAnalyzeUserService _analyzeUserService;
        private readonly UserManager<IdentityUser> _userManager;

        public AnalyzeUserStateEndpoint(
            IAnalyzeUserService analyzeUserService,
            UserManager<IdentityUser> userManager)
        {
            _analyzeUserService = analyzeUserService;
            _userManager = userManager;
        }


        [HttpPost("api/ai/analyze-user-state")]
        [EndpointDescription("Analyze user state using AI based on user and Oura data.")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<string>> HandleAsync(DateOnly request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Unauthorized("User is not recognized or no longer exists.");

            var result = await _analyzeUserService.AnalyzeUserState(user, request);
            return this.ToActionResult(result);
        }
    }
}