using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.Training;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.DTOs;

namespace lionheart.Endpoints.ProgramEndpoints
{
    [ValidateModel]
    public class GetTrainingProgramsEndpoint : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<List<TrainingProgramDTO>>
    {
        private readonly ITrainingProgramService _trainingProgramService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetTrainingProgramsEndpoint(ITrainingProgramService trainingProgramService, UserManager<IdentityUser> userManager)
        {
            _trainingProgramService = trainingProgramService;
            _userManager = userManager;
        }

        [HttpGet("api/training-program/get-all")]
        [EndpointDescription("Get all training programs for the authenticated user.")]
        [ProducesResponseType<List<TrainingProgramDTO>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<List<TrainingProgramDTO>>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _trainingProgramService.GetTrainingProgramsAsync(user));
        }
    }
}