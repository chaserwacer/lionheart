using System.ComponentModel.DataAnnotations;
using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.PersonalRecords;

/// <summary>
/// Which chain to read, as the query string states it.
/// </summary>
/// <remarks>
/// Settable properties rather than the domain's own <see cref="RecordHistoryQuery"/>, because
/// MVC's query binder writes through setters. It converts to the domain record, which is where a
/// kind's spelling and its bracket are checked together.
/// </remarks>
public class PersonalRecordHistoryRequest
{
    /// <summary>Absent means trail running, which is held against no movement.</summary>
    [FromQuery]
    public Guid? MovementDataId { get; set; }

    [FromQuery]
    [Required]
    public string Kind { get; set; } = string.Empty;

    /// <summary>The rep count for a rep max, or the distance in meters for a fastest time.</summary>
    [FromQuery]
    public int? Bracket { get; set; }

    public RecordHistoryQuery ToQuery() => new(MovementDataId, Kind, Bracket);
}

/// <summary>A subject's whole progression on one kind of record.</summary>
[ValidateModel]
public class GetPersonalRecordHistoryEndpoint : EndpointBaseAsync
    .WithRequest<PersonalRecordHistoryRequest>
    .WithActionResult<IReadOnlyList<PersonalRecordView>>
{
    private readonly IPersonalRecordService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetPersonalRecordHistoryEndpoint(
        IPersonalRecordService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/personal-records/history")]
    [EndpointDescription("Every record ever set for one subject and kind, newest first — including the ones since beaten and the ones withdrawn.")]
    [ProducesResponseType<IReadOnlyList<PersonalRecordView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<IReadOnlyList<PersonalRecordView>>> HandleAsync(
        [FromQuery] PersonalRecordHistoryRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetHistoryAsync(user, request.ToQuery(), cancellationToken));
    }
}
