using Ardalis.ApiEndpoints;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.InjuryEndpoints
{
    [ValidateModel]
    public class DeleteInjuryEndpoint : EndpointBaseAsync
        .WithRequest<Guid>
        .WithActionResult
    {
        private readonly IInjuryService _injuryService;
        private readonly UserManager<IdentityUser> _userManager;

        public DeleteInjuryEndpoint(IInjuryService injuryService, UserManager<IdentityUser> userManager)
        {
            _injuryService = injuryService;
            _userManager = userManager;
        }

        [HttpDelete("api/injury/delete/{injuryId}")]
        [EndpointDescription("Delete an injury by ID.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult> HandleAsync([FromRoute] Guid injuryId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized("User is not recognized or no longer exists.");

            var result = await _injuryService.DeleteInjuryAsync(user, injuryId);
            return this.ToActionResult(result);
        }
    }
}
