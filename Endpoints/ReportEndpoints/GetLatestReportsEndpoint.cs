using Ardalis.ApiEndpoints;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.Report;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ReportEndpoints;

/// <summary>
/// Endpoint to get the latest report of each type (for dashboard display).
/// </summary>
[ValidateModel]
public class GetLatestReportsEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<Dictionary<ReportType, Report.ReportDTO>>
{
    private readonly IReportService _reportService;
    private readonly UserManager<IdentityUser> _userManager;

    public GetLatestReportsEndpoint(IReportService reportService, UserManager<IdentityUser> userManager)
    {
        _reportService = reportService;
        _userManager = userManager;
    }

    [HttpGet("api/report/latest")]
    [EndpointDescription("Get the latest report of each type for dashboard display.")]
    [ProducesResponseType<Dictionary<ReportType, Report.ReportDTO>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<Dictionary<ReportType, Report.ReportDTO>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _reportService.GetLatestReportsAsync(user));
    }
}
