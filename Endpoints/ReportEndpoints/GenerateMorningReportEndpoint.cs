using Ardalis.ApiEndpoints;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.Report;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ReportEndpoints;

/// <summary>
/// Endpoint to generate a morning report.
/// Morning reports focus on sleep quality and readiness for the day ahead.
/// </summary>
[ValidateModel]
public class GenerateMorningReportEndpoint : EndpointBaseAsync
    .WithRequest<GenerateMorningReportRequest>
    .WithActionResult<Report.ReportDTO>
{
    private readonly IReportService _reportService;
    private readonly UserManager<IdentityUser> _userManager;

    public GenerateMorningReportEndpoint(IReportService reportService, UserManager<IdentityUser> userManager)
    {
        _reportService = reportService;
        _userManager = userManager;
    }

    [HttpPost("api/report/generate/morning")]
    [EndpointDescription("Generate a morning report analyzing sleep and readiness for the day.")]
    [ProducesResponseType<Report.ReportDTO>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<Report.ReportDTO>> HandleAsync(
        [FromBody] GenerateMorningReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _reportService.GenerateMorningReportAsync(user, request));
    }
}
