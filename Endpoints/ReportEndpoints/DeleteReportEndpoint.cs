using Ardalis.ApiEndpoints;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ReportEndpoints;

/// <summary>
/// Endpoint to delete a report.
/// </summary>
[ValidateModel]
public class DeleteReportEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult
{
    private readonly IReportService _reportService;
    private readonly UserManager<IdentityUser> _userManager;

    public DeleteReportEndpoint(IReportService reportService, UserManager<IdentityUser> userManager)
    {
        _reportService = reportService;
        _userManager = userManager;
    }

    [HttpDelete("api/report/{reportId:guid}")]
    [EndpointDescription("Delete a report.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult> HandleAsync(
        [FromRoute] Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _reportService.DeleteReportAsync(user, reportId));
    }
}
