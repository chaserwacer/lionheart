using Ardalis.ApiEndpoints;
using Ardalis.Result;
using lionheart.Data;
using Ardalis.Result.AspNetCore;
using lionheart.Model.DTOs;
using lionheart.Model.TrainingProgram;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Endpoints.TrainingSession
{
    [Route("api/training-session")]
    public class GenerateTrainingSessionsEndpoint
      : EndpointBaseAsync
          .WithRequest<GenerateTrainingSessionsRequest>
          .WithActionResult<List<TrainingSessionDTO>>
    {
        private readonly ModelContext _context;
        private readonly UserManager<IdentityUser> _users;
        private readonly ITrainingSessionService _sessions;

        public GenerateTrainingSessionsEndpoint(
            ModelContext context,
            UserManager<IdentityUser> users,
            ITrainingSessionService sessions)
        {
            _context  = context;
            _users    = users;
            _sessions = sessions;
        }

        [HttpPost("generate")]
        public override async Task<ActionResult<List<TrainingSessionDTO>>> HandleAsync(
            [FromBody] GenerateTrainingSessionsRequest req,
            CancellationToken ct = default)
        {
            var user = await _users.GetUserAsync(User);
            if (user is null) return Unauthorized();

            var result = await _sessions.GenerateTrainingSessionsAsync(user, req);
            return this.ToActionResult(result);
        }
    }
}
