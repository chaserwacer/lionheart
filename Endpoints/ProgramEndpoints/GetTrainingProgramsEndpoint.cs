using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.ProgramEndpoints
{
    [ValidateModel]
    public class GetTrainingProgramsEndpoint : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<List<TrainingProgram>>
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
        [ProducesResponseType<List<TrainingProgram>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<List<TrainingProgram>>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _trainingProgramService.GetTrainingProgramsAsync(user));
        }
    }
}