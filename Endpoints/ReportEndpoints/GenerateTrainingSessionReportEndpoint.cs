using Ardalis.ApiEndpoints;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.Report;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ReportEndpoints;

/// <summary>
/// Endpoint to generate a training session report.
/// Training session reports analyze performance, identify PRs, and provide insights
/// on what factors may have contributed to good or bad performance.
/// </summary>
[ValidateModel]
public class GenerateTrainingSessionReportEndpoint : EndpointBaseAsync
    .WithRequest<GenerateTrainingSessionReportRequest>
    .WithActionResult<Report.ReportDTO>
{
    private readonly IReportService _reportService;
    private readonly UserManager<IdentityUser> _userManager;

    public GenerateTrainingSessionReportEndpoint(IReportService reportService, UserManager<IdentityUser> userManager)
    {
        _reportService = reportService;
        _userManager = userManager;
    }

    [HttpPost("api/report/generate/training-session")]
    [EndpointDescription("Generate a training session report analyzing performance and PRs.")]
    [ProducesResponseType<Report.ReportDTO>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<Report.ReportDTO>> HandleAsync(
        [FromBody] GenerateTrainingSessionReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _reportService.GenerateTrainingSessionReportAsync(user, request));
    }
}
