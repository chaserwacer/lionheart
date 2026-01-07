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
    public class UpdateLiftSetEntryEndpoint : EndpointBaseAsync
        .WithRequest<UpdateLiftSetEntryRequest>
        .WithActionResult<LiftSetEntryDTO>
    {
        private readonly ILiftSetEntryService _liftSetEntryService;
        private readonly UserManager<IdentityUser> _userManager;

        public UpdateLiftSetEntryEndpoint(ILiftSetEntryService liftSetEntryService, UserManager<IdentityUser> userManager)
        {
            _liftSetEntryService = liftSetEntryService;
            _userManager = userManager;
        }

        [HttpPut("api/lift-set-entry/update")]
        [EndpointDescription("Update an existing lift set entry.")]
        [ProducesResponseType<LiftSetEntryDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<LiftSetEntryDTO>> HandleAsync([FromBody] UpdateLiftSetEntryRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _liftSetEntryService.UpdateLiftSetEntryAsync(user, request));
        }
    }
}
