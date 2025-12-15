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
    public class CreateDTSetEntryEndpoint : EndpointBaseAsync
        .WithRequest<CreateDTSetEntryRequest>
        .WithActionResult<DTSetEntryDTO>
    {
        private readonly IDTSetEntryService _dtSetEntryService;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateDTSetEntryEndpoint(IDTSetEntryService dtSetEntryService, UserManager<IdentityUser> userManager)
        {
            _dtSetEntryService = dtSetEntryService;
            _userManager = userManager;
        }

        [HttpPost("api/dt-set-entry/create")]
        [EndpointDescription("Create a new distance/time set entry within a movement.")]
        [ProducesResponseType<DTSetEntryDTO>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<DTSetEntryDTO>> HandleAsync([FromBody] CreateDTSetEntryRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _dtSetEntryService.CreateDTSetEntryAsync(user, request));
        }
    }
}
