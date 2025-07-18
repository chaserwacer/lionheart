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
    public class GenerateProgramFirstWeekEndpoint : EndpointBaseAsync
        .WithRequest<FirstWeekGenerationDTO>
        .WithActionResult<string>
    {
        private readonly IProgramGenerationService _service;
        private readonly UserManager<IdentityUser> _userManager;

        public GenerateProgramFirstWeekEndpoint(IProgramGenerationService service, UserManager<IdentityUser> userManager)
        {
            _service = service;
            _userManager = userManager;
        }

        [HttpPost("api/ai/program/first-week")]
        public override async Task<ActionResult<string>> HandleAsync(
            FirstWeekGenerationDTO request,
            CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Unauthorized();

            var result = await _service.GenerateFirstWeekAsync(user, request);
            if (!result.IsSuccess)
            {
                Console.WriteLine($"FirstWeek generation failed: {string.Join(" | ", result.Errors)}");
                return BadRequest(result.Errors);
            }


            return result.IsSuccess
                ? Content(result.Value, "text/plain") // ✅ explicitly typed as string content
                : BadRequest(result.Errors);
        }


    }
}
