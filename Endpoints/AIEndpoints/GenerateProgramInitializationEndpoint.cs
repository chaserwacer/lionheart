using Ardalis.ApiEndpoints;
using Ardalis.Result;
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
    public class GenerateProgramInitializationEndpoint : EndpointBaseAsync
        .WithoutRequest
        .WithActionResult<string>
    {
        private readonly IProgramGenerationService _service;
        private readonly UserManager<IdentityUser> _userManager;

        public GenerateProgramInitializationEndpoint(IProgramGenerationService service, UserManager<IdentityUser> userManager)
        {
            _service = service;
            _userManager = userManager;
        }

        [HttpPost("api/ai/program/init")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [Produces("text/plain")]    
        public override async Task<ActionResult<string>> HandleAsync(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Unauthorized();

            var result = await _service.GenerateInitializationAsync(user);
            return this.ToActionResult(result);
        }
    }
}
