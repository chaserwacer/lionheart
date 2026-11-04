using Ardalis.ApiEndpoints;
using lionheart.Functional;
using lionheart.Model.Profile;
using lionheart.Services.Chat.Today;
using lionheart.Services.Profile;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace lionheart.Endpoints.ProfileEndpoints;

/// <summary>
/// Hands the athlete the context the chat model is given about them: the card a conversation
/// opens with, and the briefing the next message would go with.
/// </summary>
/// <remarks>
/// An answer is only worth trusting if its premises are inspectable, and these are the premise
/// behind every one of them. That is what this route is for.
/// <para>
/// It is a read that can build: <see cref="IAthleteContextCardService.GetOrBuildCardAsync"/>
/// materialises a missing card, and rebuilding the stable tier attempts one best-effort narrative
/// sentence from the cheap model. So this route is occasionally slow and — where that model is a
/// local server that serves one request at a time — must not be called alongside a chat exchange.
/// </para>
/// </remarks>
public class GetAthleteContextCardEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult<AthleteContextCardView>
{
    private readonly IAthleteContextCardService _cardService;
    private readonly ITodayBriefing _briefing;
    private readonly UserManager<IdentityUser> _userManager;

    public GetAthleteContextCardEndpoint(
        IAthleteContextCardService cardService, ITodayBriefing briefing, UserManager<IdentityUser> userManager)
    {
        _cardService = cardService;
        _briefing = briefing;
        _userManager = userManager;
    }

    [HttpGet("api/profile/context-card")]
    [EndpointDescription("The standing summary of this athlete that the chat model is given, and today's briefing as the next message would carry it.")]
    [ProducesResponseType<AthleteContextCardView>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public override async Task<ActionResult<AthleteContextCardView>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) { return Unauthorized("User is not recognized or no longer exists."); }

        return this.ToActionResult(
            await _cardService.GetOrBuildCardAsync(user)
                .MapAsync(async card => AthleteContextCardView.Of(card, await _briefing.ForAsync(user, cancellationToken))));
    }
}
