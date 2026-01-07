using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Services.Training;

namespace lionheart.Endpoints.Training.Movement
{
    [ValidateModel]

    public class GetAllMuscleGroupsAsync : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<List<MuscleGroup>>
    {
        private readonly IMovementBaseService _movementBaseService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetAllMuscleGroupsAsync(IMovementBaseService movementBaseService, UserManager<IdentityUser> userManager)
        {
            _movementBaseService = movementBaseService;
            _userManager = userManager;
        }

        [HttpGet("api/muscle-groups/get-all")]
        [EndpointDescription("Get all available muscle groups.")]
        [ProducesResponseType<List<MuscleGroup>>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<List<MuscleGroup>>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _movementBaseService.GetAllMuscleGroupsAsync());
        }
    }
}