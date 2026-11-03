using Ardalis.ApiEndpoints;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.PersonalRecords;

/// <summary>The standing records grouped by the subject they are held against.</summary>
public class GetPersonalRecordSummariesEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<IReadOnlyList<SubjectRecordsView>>
{
    private readonly IPersonalRecordService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetPersonalRecordSummariesEndpoint(
        IPersonalRecordService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/personal-records/summaries")]
    [EndpointDescription("The athlete's standing records, grouped by the movement they are held on — or by trail running, which holds no movement.")]
    [ProducesResponseType<IReadOnlyList<SubjectRecordsView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<IReadOnlyList<SubjectRecordsView>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(await _service.GetBySubjectAsync(user, cancellationToken));
    }
}
