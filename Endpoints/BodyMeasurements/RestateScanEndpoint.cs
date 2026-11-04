using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.BodyMeasurements.Composition;
using lionheart.Services.BodyComposition;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.BodyMeasurements;

/// <summary>Replaces what was entered for a scan with a corrected reading of its sheet.</summary>
[ValidateModel]
public class RestateScanEndpoint : EndpointBaseAsync
    .WithRequest<Addressed<ScanRequest>>
    .WithActionResult<ScanView>
{
    private readonly IBodyCompositionService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public RestateScanEndpoint(IBodyCompositionService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPut("api/body-composition/scans/{id:guid}")]
    [EndpointDescription("Correct a recorded body-composition scan, replacing its sheet whole.")]
    [ProducesResponseType<ScanView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<ScanView>> HandleAsync(Addressed<ScanRequest> request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.RestateAsync(user, request.Id, request.Body, cancellationToken),
            StatusCodes.Status200OK);
    }
}
