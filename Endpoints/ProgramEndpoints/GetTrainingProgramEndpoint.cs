using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.DTOs;

namespace lionheart.Endpoints.ProgramEndpoints
{
    [ValidateModel]
    public class GetTrainingProgramEndpoint : EndpointBaseAsync
        .WithRequest<Guid>
        .WithActionResult<TrainingProgramDTO>
    {
        private readonly ITrainingProgramService _trainingProgramService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetTrainingProgramEndpoint(ITrainingProgramService trainingProgramService, UserManager<IdentityUser> userManager)
        {
            _trainingProgramService = trainingProgramService;
            _userManager = userManager;
        }

        [HttpGet("api/training-program/get/{programId}")]
        [EndpointDescription("Get a specific training program by ID.")]
        [ProducesResponseType<TrainingProgramDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<TrainingProgramDTO>> HandleAsync([FromRoute] Guid programId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _trainingProgramService.GetTrainingProgramAsync(user, programId));
        }
    }
}