using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Model.DTOs;
using lionheart.Services.AI;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading;
using System.Threading.Tasks;

namespace lionheart.Endpoints.AIEndpoints
{
    [Authorize]
    public class GenerateProgramPreferencesEndpoint : EndpointBaseAsync
        .WithRequest<ProgramPreferencesDTO>
        .WithActionResult
    {
        private readonly IProgramGenerationService _service;
        private readonly UserManager<IdentityUser> _userManager;

        public GenerateProgramPreferencesEndpoint(IProgramGenerationService service, UserManager<IdentityUser> userManager)
        {
            _service = service;
            _userManager = userManager;
        }

        [HttpPost("api/ai/program/preferences")]
        public override async Task<ActionResult> HandleAsync(ProgramPreferencesDTO request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Unauthorized();

            var result = await _service.GeneratePreferencesAsync(user, request);

            return result.IsSuccess
                ? Ok(result.Value) // Sends plain text string as HTTP 200 with correct type
                : BadRequest(result.Errors);
        }
    }
}
