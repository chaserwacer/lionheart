using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Interpretation;
using lionheart.Services.Interpretation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Interpretation;

[ValidateModel]
public class InterpretTextEndpoint : EndpointBaseAsync
    .WithRequest<InterpretationRequest>
    .WithActionResult<InterpretationOutcome>
{
    private readonly IInterpretationEngine _interpretationEngine;
    private readonly UserManager<IdentityUser> _userManager;

    public InterpretTextEndpoint(
        IInterpretationEngine interpretationEngine, UserManager<IdentityUser> userManager)
    {
        _interpretationEngine = interpretationEngine;
        _userManager = userManager;
    }

    /// <remarks>
    /// Both outcomes are a 200: being asked to disambiguate a name is how the run ended, not
    /// a reason the request was refused. The body names which case it is.
    /// </remarks>
    [HttpPost("api/interpretation/interpret")]
    [EndpointDescription("Interpret a training log entry, committing it or asking which names were meant.")]
    [ProducesResponseType<InterpretationOutcome>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public override async Task<ActionResult<InterpretationOutcome>> HandleAsync(
        [FromBody] InterpretationRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _interpretationEngine.InterpretAsync(user, request));
    }
}
