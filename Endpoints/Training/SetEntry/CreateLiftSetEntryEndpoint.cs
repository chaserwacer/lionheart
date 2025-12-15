using Ardalis.ApiEndpoints;
using lionheart.Services;
using lionheart.Model.Training.SetEntry;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.SetEntryEndpoints
{
    [ValidateModel]
    public class CreateLiftSetEntryEndpoint : EndpointBaseAsync
        .WithRequest<CreateLiftSetEntryRequest>
        .WithActionResult<LiftSetEntryDTO>
    {
        private readonly ILiftSetEntryService _liftSetEntryService;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateLiftSetEntryEndpoint(ILiftSetEntryService liftSetEntryService, UserManager<IdentityUser> userManager)
        {
            _liftSetEntryService = liftSetEntryService;
            _userManager = userManager;
        }

        [HttpPost("api/lift-set-entry/create")]
        [EndpointDescription("Create a new lift set entry within a movement.")]
        [ProducesResponseType<LiftSetEntryDTO>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<LiftSetEntryDTO>> HandleAsync([FromBody] CreateLiftSetEntryRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _liftSetEntryService.CreateLiftSetEntryAsync(user, request));
        }
    }
}
