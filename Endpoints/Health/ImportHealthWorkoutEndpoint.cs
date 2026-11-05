using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Functional;
using lionheart.Model.Health;
using lionheart.Services.Health.Import;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Health;

/// <summary>Offers one Apple Health workout for import; answers what became of it.</summary>
[ValidateModel]
public class ImportHealthWorkoutEndpoint : EndpointBaseAsync
    .WithRequest<HealthWorkoutImport>
    .WithActionResult<HealthImportOutcome>
{
    private readonly IHealthWorkoutImportService _service;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<ImportHealthWorkoutEndpoint> _logger;

    public ImportHealthWorkoutEndpoint(
        IHealthWorkoutImportService service, UserManager<IdentityUser> userManager, ILogger<ImportHealthWorkoutEndpoint> logger)
    {
        _service = service;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpPost("api/health/workouts")]
    [EndpointDescription("Import an Apple Health workout: a session with its route and heart rate, or why it was not taken. Idempotent on the workout's Health UUID.")]
    [ProducesResponseType<HealthImportOutcome>(StatusCodes.Status201Created)]
    [ProducesResponseType<HealthImportOutcome>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<HealthImportOutcome>> HandleAsync(
        [FromBody] HealthWorkoutImport request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        // The phone holds a refused workout back and offers it again on every sync, showing only that
        // one was refused; the reason is kept here, where it can be read.
        return this.ToActionResult(
            await _service.ImportAsync(user, request, cancellationToken)
                .TapErrorAsync(problem => _logger.LogWarning(
                    "Health workout {WorkoutId} ({Activity} from {App}, {Start:u}) refused: {Reason}",
                    request.WorkoutId, request.Activity, request.AppBundleId, request.Start, problem.Description)),
            outcome => outcome,
            outcome => outcome is HealthImportOutcome.Imported ? StatusCodes.Status201Created : StatusCodes.Status200OK);
    }
}
