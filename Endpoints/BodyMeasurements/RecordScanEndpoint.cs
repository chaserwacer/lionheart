using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.BodyMeasurements.Composition;
using lionheart.Services.BodyComposition;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.BodyMeasurements;

/// <summary>Records a body-composition scan, and the bodyweight it measured when asked to.</summary>
[ValidateModel]
public class RecordScanEndpoint : EndpointBaseAsync
    .WithRequest<ScanRequest>
    .WithActionResult<ScanView>
{
    private readonly IBodyCompositionService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public RecordScanEndpoint(IBodyCompositionService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPost("api/body-composition/scans")]
    [EndpointDescription("Record a body-composition scan from its result sheet.")]
    [ProducesResponseType<ScanView>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public override async Task<ActionResult<ScanView>> HandleAsync([FromBody] ScanRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.RecordAsync(user, request, cancellationToken),
            StatusCodes.Status201Created);
    }
}
