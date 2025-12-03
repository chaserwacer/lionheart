using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Services;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.TrainingProgram.SetEntry;

namespace lionheart.Endpoints.SetEntryEndpoints
{
    [ValidateModel]
    public class UpdateSetEntryEndpoint : EndpointBaseAsync
        .WithRequest<IUpdateSetEntryRequest>
        .WithActionResult<ISetEntryDTO>
    {
        private readonly ISetEntryService _setEntryService;
        private readonly UserManager<IdentityUser> _userManager;

        public UpdateSetEntryEndpoint(ISetEntryService setEntryService, UserManager<IdentityUser> userManager)
        {
            _setEntryService = setEntryService;
            _userManager = userManager;
        }

        [HttpPut("api/set-entry/update")]
        [EndpointDescription("Update an existing set entry.")]
        [ProducesResponseType<ISetEntryDTO>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public override async Task<ActionResult<ISetEntryDTO>> HandleAsync([FromBody] IUpdateSetEntryRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _setEntryService.UpdateSetEntryAsync(user, request));
        }
    }
}