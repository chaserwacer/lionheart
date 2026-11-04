using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.BodyMeasurements.Composition;
using lionheart.Services.BodyComposition;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.BodyMeasurements;

/// <summary>Every body-composition scan, oldest first.</summary>
/// <remarks>All of them, unpaged: scans come weeks apart, so a decade of them is a few hundred.</remarks>
[ValidateModel]
public class GetScansEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<IReadOnlyList<ScanView>>
{
    private readonly IBodyCompositionService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetScansEndpoint(IBodyCompositionService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/body-composition/scans")]
    [EndpointDescription("Get every body-composition scan, oldest first.")]
    [ProducesResponseType<IReadOnlyList<ScanView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<IReadOnlyList<ScanView>>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetAllAsync(user, cancellationToken),
            StatusCodes.Status200OK);
    }
}
