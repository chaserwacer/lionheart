using Ardalis.ApiEndpoints;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.Report;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ReportEndpoints;

/// <summary>
/// Endpoint to generate an evening report.
/// Evening reports reflect on the day's activities and recovery needs.
/// </summary>
[ValidateModel]
public class GenerateEveningReportEndpoint : EndpointBaseAsync
    .WithRequest<GenerateEveningReportRequest>
    .WithActionResult<Report.ReportDTO>
{
    private readonly IReportService _reportService;
    private readonly UserManager<IdentityUser> _userManager;

    public GenerateEveningReportEndpoint(IReportService reportService, UserManager<IdentityUser> userManager)
    {
        _reportService = reportService;
        _userManager = userManager;
    }

    [HttpPost("api/report/generate/evening")]
    [EndpointDescription("Generate an evening report reflecting on the day's activities.")]
    [ProducesResponseType<Report.ReportDTO>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<Report.ReportDTO>> HandleAsync(
        [FromBody] GenerateEveningReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _reportService.GenerateEveningReportAsync(user, request));
    }
}
