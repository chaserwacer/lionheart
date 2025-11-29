using Ardalis.ApiEndpoints;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.Report;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ReportEndpoints;

/// <summary>
/// Endpoint to regenerate an existing report with fresh data.
/// </summary>
[ValidateModel]
public class RegenerateReportEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<Report.ReportDTO>
{
    private readonly IReportService _reportService;
    private readonly UserManager<IdentityUser> _userManager;

    public RegenerateReportEndpoint(IReportService reportService, UserManager<IdentityUser> userManager)
    {
        _reportService = reportService;
        _userManager = userManager;
    }

    [HttpPost("api/report/regenerate/{reportId:guid}")]
    [EndpointDescription("Regenerate an existing report with fresh data.")]
    [ProducesResponseType<Report.ReportDTO>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<Report.ReportDTO>> HandleAsync(
        [FromRoute] Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _reportService.RegenerateReportAsync(user, reportId));
    }
}
