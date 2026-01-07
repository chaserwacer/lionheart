using Ardalis.ApiEndpoints;
using lionheart.WellBeing;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.Request;

namespace lionheart.Endpoints.WellnessEndpoints
{
    [ValidateModel]
    public class GetWellnessStatesEndpoint : EndpointBaseAsync
        .WithRequest<DateRangeRequest>
        .WithActionResult<List<WellnessState>>
    {
        private readonly IWellnessService _wellnessService;
        private readonly UserManager<IdentityUser> _userManager;

        public GetWellnessStatesEndpoint(IWellnessService wellnessService, UserManager<IdentityUser> userManager)
        {
            _wellnessService = wellnessService;
            _userManager = userManager;
        }

        [HttpGet("api/wellness/get-range")]
        [EndpointDescription("Select all WellnessStates for the user within the specified date range.")]
        [ProducesResponseType<List<WellnessState>>(StatusCodes.Status200OK)]
        [ProducesResponseType<List<WellnessState>>(StatusCodes.Status401Unauthorized)]
        public override async Task<ActionResult<List<WellnessState>>> HandleAsync([FromQuery] DateRangeRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

            return this.ToActionResult(await _wellnessService.GetWellnessStatesAsync(user, request));
        }
    }
}