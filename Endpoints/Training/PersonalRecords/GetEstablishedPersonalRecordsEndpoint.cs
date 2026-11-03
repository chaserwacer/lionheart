using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Training.PersonalRecords;
using lionheart.Services.Training.PersonalRecords;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Training.PersonalRecords;

/// <summary>
/// Which established records to read, as the query string states it. Settable for MVC's query
/// binder, for the reason <see cref="PersonalRecordHistoryRequest"/> is.
/// </summary>
public class EstablishedPersonalRecordsRequest
{
    [FromQuery]
    public Guid? MovementDataId { get; set; }

    /// <summary>The earliest instant a record was achieved at, included.</summary>
    [FromQuery]
    public DateTimeOffset? From { get; set; }

    /// <summary>The latest instant a record was achieved at, included.</summary>
    [FromQuery]
    public DateTimeOffset? To { get; set; }

    public EstablishedRecordsQuery ToQuery() => new(MovementDataId, From, To);
}

/// <summary>The records a movement or a window established, whatever became of them since.</summary>
[ValidateModel]
public class GetEstablishedPersonalRecordsEndpoint : EndpointBaseAsync
    .WithRequest<EstablishedPersonalRecordsRequest>
    .WithActionResult<IReadOnlyList<PersonalRecordView>>
{
    private readonly IPersonalRecordService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public GetEstablishedPersonalRecordsEndpoint(
        IPersonalRecordService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpGet("api/personal-records/established")]
    [EndpointDescription("Every record set on a movement and/or achieved inside [from, to], newest first — standing, beaten and retracted alike. A session's records are the window from its start to its start.")]
    [ProducesResponseType<IReadOnlyList<PersonalRecordView>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<IReadOnlyList<PersonalRecordView>>> HandleAsync(
        [FromQuery] EstablishedPersonalRecordsRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.GetEstablishedAsync(user, request.ToQuery(), cancellationToken));
    }
}
