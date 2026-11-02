using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Model.Oura;
using lionheart.Services.Oura;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Oura;

/// <summary>
/// Every day in a range the ring recorded anything for, each with all six of its daily documents.
/// </summary>
/// <remarks>
/// A day range rather than a <see cref="TimeWindow"/>: these documents are keyed by the calendar
/// day Oura filed them under, so asking in instants would make the caller invent a time zone for
/// midnight. Days the ring recorded nothing for are absent rather than present and empty.
/// </remarks>
[ValidateModel]
public class GetOuraDaysEndpoint : EndpointBaseAsync
    .WithRequest<DayRange>
    .WithActionResult<IReadOnlyList<OuraDayView>>
{
    private readonly IOuraReadService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetOuraDaysEndpoint(IOuraReadService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/oura/days")]
    [EndpointDescription("Get the Oura daily documents for a range of days.")]
    [ProducesResponseType<IReadOnlyList<OuraDayView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public override async Task<ActionResult<IReadOnlyList<OuraDayView>>> HandleAsync(
        [FromQuery] DayRange request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetDaysAsync(user, request, cancellationToken),
            StatusCodes.Status200OK);
    }
}
