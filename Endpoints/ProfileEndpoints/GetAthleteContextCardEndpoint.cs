using Ardalis.ApiEndpoints;
using lionheart.Functional;
using lionheart.Model.Profile;
using lionheart.Services.Profile;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ProfileEndpoints;

/// <summary>
/// Hands the athlete the context card the chat model is given about them.
/// </summary>
/// <remarks>
/// The card was previously reachable only through the system prompt, so the athlete could read the
/// model's conclusions but not the standing summary they were drawn from. A coaching answer is only
/// worth trusting if its premises are inspectable, which is what this route is for.
/// <para>
/// It is a read that can build: <see cref="IAthleteContextCardService.GetOrBuildCardAsync"/>
/// materialises a missing card and refreshes a stale Recent State, and each rebuild attempts one
/// best-effort narrative sentence from the cheap model. So this route is occasionally slow and —
/// where that model is a local server that serves one request at a time — must not be called
/// alongside a chat exchange.
/// </para>
/// </remarks>
public class GetAthleteContextCardEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<AthleteContextCardView>
{
    private readonly IAthleteContextCardService _cardService;
    private readonly UserManager<IdentityUser> _userManager;

    public GetAthleteContextCardEndpoint(
        IAthleteContextCardService cardService, UserManager<IdentityUser> userManager)
    {
        _cardService = cardService;
        _userManager = userManager;
    }

    [HttpGet("api/profile/context-card")]
    [EndpointDescription("The standing summary of this athlete that the chat model is given.")]
    [ProducesResponseType<AthleteContextCardView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<AthleteContextCardView>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

        return this.ToActionResult(
            await _cardService.GetOrBuildCardAsync(user).MapAsync(AthleteContextCardView.Of));
    }
}
