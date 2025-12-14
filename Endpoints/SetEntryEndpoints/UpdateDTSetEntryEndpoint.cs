using Ardalis.ApiEndpoints;
using lionheart.Services;
using lionheart.Model.TrainingProgram.SetEntry;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.SetEntryEndpoints
{
    [ValidateModel]
    public class UpdateDTSetEntryEndpoint : EndpointBaseAsync
        .WithRequest<UpdateDTSetEntryRequest>
        .WithActionResult<DTSetEntryDTO>
    {
        private readonly IDTSetEntryService _dtSetEntryService;
        private readonly UserManager<IdentityUser> _userManager;

        public UpdateDTSetEntryEndpoint(IDTSetEntryService dtSetEntryService, UserManager<IdentityUser> userManager)
        {
            _dtSetEntryService = dtSetEntryService;
            _userManager = userManager;
        }

        [HttpPut("api/dt-set-entry/update")]
        [EndpointDescription("Update an existing distance/time set entry.")]
        [ProducesResponseType<DTSetEntryDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<DTSetEntryDTO>> HandleAsync([FromBody] UpdateDTSetEntryRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _dtSetEntryService.UpdateDTSetEntryAsync(user, request));
        }
    }
}
