using Ardalis.ApiEndpoints;
using lionheart.Endpoints.Filters;
using lionheart.Model.Contexts;
using lionheart.Services.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.Contexts;

/// <summary>Notes are commentary, editable without changing what kind of context this is.</summary>
[ValidateModel]
public class UpdateContextNotesEndpoint : EndpointBaseAsync
    .WithRequest<Addressed<ContextNotesRequest>>
    .WithActionResult<ContextView>
{
    private readonly IContextService _service;
    private readonly UserManager<IdentityUser> _userManager;

    public UpdateContextNotesEndpoint(IContextService service, UserManager<IdentityUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    [HttpPatch("api/contexts/{id:guid}/notes")]
    [EndpointDescription("Update a context's notes.")]
    [ProducesResponseType<ContextView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public override async Task<ActionResult<ContextView>> HandleAsync(Addressed<ContextNotesRequest> request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Unauthorized("User is not recognized or no longer exists.");

        return this.ToActionResult(
            await _service.UpdateNotesAsync(user, request.Id, request.Body, cancellationToken),
            StatusCodes.Status200OK);
    }
}
