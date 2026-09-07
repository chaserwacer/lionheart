using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Interpretation;
using lionheart.Services.Interpretation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Interpretation;

[ValidateModel]
public class ConfirmInterpretationEndpoint : EndpointBaseAsync
    .WithRequest<ConfirmationRequest>
    .WithActionResult<InterpretationOutcome>
{
    private readonly IInterpretationEngine _interpretationEngine;
    private readonly UserManager<IdentityUser> _userManager;

    public ConfirmInterpretationEndpoint(
        IInterpretationEngine interpretationEngine, UserManager<IdentityUser> userManager)
    {
        _interpretationEngine = interpretationEngine;
        _userManager = userManager;
    }

    /// <remarks>
    /// The client sends back the draft it was given, so there is no server-side pending
    /// state to look up and the text is never parsed twice. Answering only some of the names
    /// is legitimate: the run comes back awaiting confirmation of the rest.
    /// </remarks>
    [HttpPost("api/interpretation/confirm")]
    [EndpointDescription("Resume an interpretation awaiting confirmation with the athlete's choice for each name.")]
    [ProducesResponseType<InterpretationOutcome>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public override async Task<ActionResult<InterpretationOutcome>> HandleAsync(
        [FromBody] ConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _interpretationEngine.ConfirmAsync(user, request));
    }
}
