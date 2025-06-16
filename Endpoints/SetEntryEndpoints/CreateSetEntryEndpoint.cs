using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;

namespace lionheart.Endpoints.SetEntryEndpoints
{
    [ValidateModel]
    public class CreateSetEntryEndpoint : EndpointBaseAsync
        .WithRequest<CreateSetEntryRequest>
        .WithActionResult<SetEntryDTO>
    {
        private readonly ISetEntryService _setEntryService;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateSetEntryEndpoint(ISetEntryService setEntryService, UserManager<IdentityUser> userManager)
        {
            _setEntryService = setEntryService;
            _userManager = userManager;
        }

        [HttpPost("api/set-entry/create")]
        [EndpointDescription("Create a new set entry within a movement.")]
        [ProducesResponseType<SetEntryDTO>(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<SetEntryDTO>> HandleAsync([FromBody] CreateSetEntryRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _setEntryService.CreateSetEntryAsync(user, request));
        }
    }
}