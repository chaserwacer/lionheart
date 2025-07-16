using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Model.DTOs;
using lionheart.Services.AI;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Ardalis.Result.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using System.Threading;
using System.Threading.Tasks;

namespace lionheart.Endpoints.AIEndpoints
{
    [Authorize]
    public class GenerateProgramRemainingWeeksEndpoint : EndpointBaseAsync
        .WithRequest<RemainingWeeksGenerationDTO>
        .WithActionResult<string>
    {
        private readonly IProgramGenerationService _service;
        private readonly UserManager<IdentityUser> _userManager;

        public GenerateProgramRemainingWeeksEndpoint(IProgramGenerationService service, UserManager<IdentityUser> userManager)
        {
            _service = service;
            _userManager = userManager;
        }

        [HttpPost("api/ai/program/continue-weeks")]
        public override async Task<ActionResult<string>> HandleAsync(RemainingWeeksGenerationDTO request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Unauthorized();

            var result = await _service.GenerateRemainingWeeksAsync(user, request);
            return this.ToActionResult(result);
        }
    }
}
