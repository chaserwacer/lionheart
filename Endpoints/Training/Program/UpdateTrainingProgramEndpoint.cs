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
    public class UpdateTrainingProgramEndpoint : EndpointBaseAsync
        .WithRequest<UpdateTrainingProgramRequest>
        .WithActionResult<TrainingProgramDTO>
    {
        private readonly ITrainingProgramService _trainingProgramService;
        private readonly UserManager<IdentityUser> _userManager;

        public UpdateTrainingProgramEndpoint(ITrainingProgramService trainingProgramService, UserManager<IdentityUser> userManager)
        {
            _trainingProgramService = trainingProgramService;
            _userManager = userManager;
        }

        [HttpPut("api/training-program/update")]
        [EndpointDescription("Update an existing training program.")]
        [ProducesResponseType<TrainingProgramDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<TrainingProgramDTO>> HandleAsync([FromBody] UpdateTrainingProgramRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _trainingProgramService.UpdateTrainingProgramAsync(user, request));
        }
    }
}