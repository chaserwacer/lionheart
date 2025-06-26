using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace lionheart.Endpoints.TrainiungSessionEndpoints
{
    [ValidateModel]
    [McpServerToolType]
    public class CreateTrainingProgramFromJSONEndpoint : EndpointBaseAsync
        .WithRequest<TrainingProgramDTO>
        .WithActionResult<TrainingProgramDTO>
    {
        private readonly ITrainingProgramService _trainingProgramService;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateTrainingProgramFromJSONEndpoint(ITrainingProgramService trainingProgramService, UserManager<IdentityUser> userManager)
        {
            _trainingProgramService = trainingProgramService;
            _userManager = userManager;
        }
        [McpServerTool, Description("Create a new populated training program from JSON data")]
        [HttpPost("api/training-program/create-from-json")]
        [EndpointDescription("Create a new populated training program")]
        [ProducesResponseType<TrainingSessionDTO>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<TrainingProgramDTO>> HandleAsync([FromBody] TrainingProgramDTO request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _trainingProgramService.CreateTrainingProgramFromJSON(user, request));
        }
    }
}