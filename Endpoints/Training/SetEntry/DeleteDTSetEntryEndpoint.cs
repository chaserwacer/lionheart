using Ardalis.ApiEndpoints;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.SetEntryEndpoints
{
    [ValidateModel]
    public class DeleteDTSetEntryEndpoint : EndpointBaseAsync
        .WithRequest<Guid>
        .WithActionResult
    {
        private readonly IDTSetEntryService _dtSetEntryService;
        private readonly UserManager<IdentityUser> _userManager;

        public DeleteDTSetEntryEndpoint(IDTSetEntryService dtSetEntryService, UserManager<IdentityUser> userManager)
        {
            _dtSetEntryService = dtSetEntryService;
            _userManager = userManager;
        }

        [HttpDelete("api/dt-set-entry/delete/{setEntryId}")]
        [EndpointDescription("Delete a distance/time set entry by ID.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult> HandleAsync([FromRoute] Guid setEntryId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _dtSetEntryService.DeleteDTSetEntryAsync(user, setEntryId));
        }
    }
}
