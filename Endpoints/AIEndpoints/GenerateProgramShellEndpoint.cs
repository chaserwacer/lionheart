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
    public class GenerateProgramShellEndpoint : EndpointBaseAsync
        .WithRequest<ProgramShellDTO>
        .WithActionResult<string>
    {
        private readonly IProgramGenerationService _service;
        private readonly UserManager<IdentityUser> _userManager;

        public GenerateProgramShellEndpoint(IProgramGenerationService service, UserManager<IdentityUser> userManager)
        {
            _service = service;
            _userManager = userManager;
        }

        [HttpPost("api/ai/program/shell")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [Produces("text/plain")]
        public override async Task<ActionResult<string>> HandleAsync(
            [FromBody] ProgramShellDTO dto, // ✅ REQUIRED for correct model binding
            CancellationToken cancellationToken = default)
        {

            Console.WriteLine("Hit Shell HandleAsync");
            // Optional: return a validation error if model binding fails
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                return BadRequest($"Model binding failed: {errors}");
            }


            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return Unauthorized();

            var result = await _service.GenerateProgramShellAsync(user, dto);
            return this.ToActionResult(result);
        }
    }
}
