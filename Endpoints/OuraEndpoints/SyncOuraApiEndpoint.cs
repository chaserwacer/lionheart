using Ardalis.ApiEndpoints;
using lionheart.Services;
using lionheart.Model.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Filters;
using Ardalis.Result.AspNetCore;

namespace lionheart.Endpoints.OuraEndpoints
{
    [ValidateModel]
    public class SyncOuraApiEndpoint : EndpointBaseAsync
        .WithRequest<DateRangeRequest>
        .WithActionResult
    {
        private readonly IOuraService _ouraService;
        private readonly UserManager<IdentityUser> _userManager;

        public SyncOuraApiEndpoint(IOuraService ouraService, UserManager<IdentityUser> userManager)
        {
            _ouraService = ouraService;
            _userManager = userManager;
        }

        [HttpPost("api/oura/sync")]
        [EndpointDescription("Synchronize Oura data for a user within a specified date range.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult> HandleAsync([FromBody] DateRangeRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _ouraService.SyncOuraAPI(user, request));
            //return NoContent();
        }
    }
}