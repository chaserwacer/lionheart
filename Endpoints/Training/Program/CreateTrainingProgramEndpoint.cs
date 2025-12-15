using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.DTOs;
using lionheart.Model.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.ProgramEndpoints
{
    [ValidateModel]
    public class CreateTrainingProgramEndpoint : EndpointBaseAsync
        .WithRequest<CreateTrainingProgramRequest>
        .WithActionResult<TrainingProgramDTO>
    { 
        private readonly ITrainingProgramService _trainingProgramService;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateTrainingProgramEndpoint(ITrainingProgramService trainingProgramService, UserManager<IdentityUser> userManager)
        {
            _trainingProgramService = trainingProgramService;
            _userManager = userManager;
        }

        [HttpPost("api/training-program/create")]
        [EndpointDescription("Create a new training program for the authenticated user.")]
        [ProducesResponseType<TrainingProgramDTO>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<TrainingProgramDTO>> HandleAsync([FromBody] CreateTrainingProgramRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _trainingProgramService.CreateTrainingProgramAsync(user, request));
        }
    }
}