using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.SetEntryEndpoints
{
    [ValidateModel]
    public class DeleteSetEntryEndpoint : EndpointBaseAsync
        .WithRequest<Guid>
        .WithActionResult
    {
        private readonly ISetEntryService _setEntryService;
        private readonly UserManager<IdentityUser> _userManager;

        public DeleteSetEntryEndpoint(ISetEntryService setEntryService, UserManager<IdentityUser> userManager)
        {
            _setEntryService = setEntryService;
            _userManager = userManager;
        }

        [HttpDelete("api/set-entry/delete/{setEntryId}")]
        [EndpointDescription("Delete a set entry by ID.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult> HandleAsync([FromRoute] Guid setEntryId, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _setEntryService.DeleteSetEntryAsync(user, setEntryId));
        }
    }
}