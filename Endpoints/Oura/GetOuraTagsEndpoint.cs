using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Analysis;
using lionheart.Model.Oura;
using lionheart.Model.Oura.Readings;
using lionheart.Services.Oura;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Oura;

/// <summary>Every tag the athlete marked on the ring inside a window.</summary>
[ValidateModel]
public class GetOuraTagsEndpoint : EndpointBaseAsync
    .WithRequest<TimeWindow>
    .WithActionResult<IReadOnlyList<OuraEventView<TagReading>>>
{
    private readonly IOuraReadService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetOuraTagsEndpoint(IOuraReadService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/oura/tags")]
    [EndpointDescription("Get the Oura tags falling inside a time window.")]
    [ProducesResponseType<IReadOnlyList<OuraEventView<TagReading>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public override async Task<ActionResult<IReadOnlyList<OuraEventView<TagReading>>>> HandleAsync(
        [FromQuery] TimeWindow request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetTagsAsync(user, request, cancellationToken),
            StatusCodes.Status200OK);
    }
}
