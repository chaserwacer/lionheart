using lionheart.Functional;
using lionheart.Services.Profile;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Services.Chat;

/// <summary>
/// The system prompt a chat opens with: the chat's voice and the athlete context card, or the
/// voice alone when the card cannot be built.
/// </summary>
/// <remarks>
/// Shared by a standalone conversation and a thread session, which both freeze what this returns
/// for their whole life.
/// </remarks>
public sealed class ChatSystemPrompt(IAthleteContextCardService cardService)
{
    /// <summary>
    /// The prompt when the card cannot be built (a new athlete with no data, or a transient failure):
    /// the same voice, but no manifest to steer tool calls, so the model is told to fetch what a
    /// question needs.
    /// </summary>
    public const string Fallback = ChatInstructions.Voice + "\n\n" +
        "Each message arrives with a TODAY note from Tempo giving the current date and time and, where it could be read, his day so far; it decides what \"today\" means. " +
        "Use the tools to retrieve anything else the question needs, and only that.";

    private readonly IAthleteContextCardService _cardService = cardService;

    /// <remarks>
    /// The catch-all is the card's own contract: the card is best-effort context, and a chat that
    /// cannot read it still answers, from the tools.
    /// </remarks>
    public async Task<string> ForAsync(IdentityUser user)
    {
        try
        {
            return (await _cardService.GetOrBuildCardAsync(user))
                .Match(_cardService.RenderSystemMessage, _ => Fallback);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Fallback;
        }
    }
}
