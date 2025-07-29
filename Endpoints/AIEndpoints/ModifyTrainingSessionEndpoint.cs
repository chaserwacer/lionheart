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
    public class ModifyTrainingSessionEndpoint : EndpointBaseAsync
        .WithRequest<ModifyTrainingSessionWithAIRequest>
        .WithActionResult<TrainingSessionDTO>
    {
        private readonly IModifyTrainingSessionService _modifyTrainingSessionService;
        private readonly UserManager<IdentityUser> _userManager;

        public ModifyTrainingSessionEndpoint(
            IModifyTrainingSessionService modifyTrainingSessionService,
            UserManager<IdentityUser> userManager)
        {
            _modifyTrainingSessionService = modifyTrainingSessionService;
            _userManager = userManager;
        }

        [HttpPost("api/ai/modify-training-session")]
        [EndpointDescription("Modify a training session using AI based on user and Oura data.")]
        [ProducesResponseType(typeof(TrainingSessionDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<TrainingSessionDTO>> HandleAsync(ModifyTrainingSessionWithAIRequest request,CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Unauthorized("User is not recognized or no longer exists.");

            var result = await _modifyTrainingSessionService.ModifySessionAsync(user, request);
            return this.ToActionResult(result);
        }
    }
}