using Ardalis.ApiEndpoints;
using Ardalis.Filters;
using Ardalis.Result.AspNetCore;
using lionheart.Model.Ingestion;
using lionheart.Services.Ingestion;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Ingestion;

[ValidateModel]
public class ParseIngestionEndpoint : EndpointBaseAsync
    .WithRequest<IngestionParseRequest>
    .WithActionResult<IngestionParseResponse>
{
    private readonly IIngestionParseService _parseService;
    private readonly UserManager<IdentityUser> _userManager;

    public ParseIngestionEndpoint(IIngestionParseService parseService, UserManager<IdentityUser> userManager)
    {
        _parseService = parseService;
        _userManager = userManager;
    }

    [HttpPost("api/ingestion/parse")]
    [EndpointDescription("Parse raw text into draft training sessions using LLM.")]
    [ProducesResponseType<IngestionParseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public override async Task<ActionResult<IngestionParseResponse>> HandleAsync(
        [FromBody] IngestionParseRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

        return this.ToActionResult(await _parseService.ParseAsync(user, request));
    }
}
