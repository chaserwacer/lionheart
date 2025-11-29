using Ardalis.ApiEndpoints;
using Ardalis.Result.AspNetCore;
using Ardalis.Filters;
using lionheart.Model.Report;
using lionheart.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ReportEndpoints;

/// <summary>
/// Endpoint to get a specific report by ID.
/// </summary>
[ValidateModel]
public class GetReportEndpoint : EndpointBaseAsync
    .WithRequest<Guid>
    .WithActionResult<Report.ReportDTO>
{
    private readonly IReportService _reportService;
    private readonly UserManager<IdentityUser> _userManager;

    public GetReportEndpoint(IReportService reportService, UserManager<IdentityUser> userManager)
    {
        _reportService = reportService;
        _userManager = userManager;
    }

    [HttpGet("api/report/{reportId:guid}")]
    [EndpointDescription("Get a specific report by ID.")]
    [ProducesResponseType<Report.ReportDTO>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<Report.ReportDTO>> HandleAsync(
        [FromRoute] Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _reportService.GetReportAsync(user, reportId));
    }
}
