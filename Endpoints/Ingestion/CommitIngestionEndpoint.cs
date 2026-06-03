using Ardalis.ApiEndpoints;
using Ardalis.Filters;
using Ardalis.Result.AspNetCore;
using lionheart.Model.Ingestion;
using lionheart.Services.Ingestion;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Ingestion;

[ValidateModel]
public class CommitIngestionEndpoint : EndpointBaseAsync
    .WithRequest<IngestionCommitRequest>
    .WithActionResult<IngestionCommitResponse>
{
    private readonly IIngestionCommitService _commitService;
    private readonly UserManager<IdentityUser> _userManager;

    public CommitIngestionEndpoint(IIngestionCommitService commitService, UserManager<IdentityUser> userManager)
    {
        _commitService = commitService;
        _userManager = userManager;
    }

    [HttpPost("api/ingestion/commit")]
    [EndpointDescription("Commit resolved draft sessions to the database.")]
    [ProducesResponseType<IngestionCommitResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public override async Task<ActionResult<IngestionCommitResponse>> HandleAsync(
        [FromBody] IngestionCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

        return this.ToActionResult(await _commitService.CommitAsync(user, request));
    }
}
