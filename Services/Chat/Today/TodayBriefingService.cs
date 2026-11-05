using System.Globalization;
using System.Text.Json;
using lionheart.Functional;
using lionheart.Services.Llm;
using Microsoft.AspNetCore.Identity;
using Model.Tools;

namespace lionheart.Services.Chat.Today;

/// <summary>The briefing a chat message is sent with.</summary>
public interface ITodayBriefing
{
    /// <summary>
    /// The athlete's day as of now. Total: when the facts cannot be read it still states the date
    /// and time, which is the one thing a briefing must never leave the model to guess.
    /// </summary>
    Task<string> ForAsync(IdentityUser user, CancellationToken ct);
}

/// <summary>
/// Reads today's facts, and the score and what is up next through their own chat tools, and hands
/// them to <see cref="TodayBriefing"/>.
/// </summary>
public sealed class TodayBriefingService(
    ITodayFacts facts,
    ChatToolCallExecutor tools,
    TimeProvider clock,
    ILogger<TodayBriefingService> logger) : ITodayBriefing
{
    private readonly ITodayFacts _facts = facts;
    private readonly ChatToolCallExecutor _tools = tools;
    private readonly TimeProvider _clock = clock;
    private readonly ILogger _logger = logger;

    /// <remarks>
    /// The catch-all is the briefing's contract, the same one the context card keeps: it is context,
    /// and a message whose context cannot be read is still answered, with the tools.
    /// </remarks>
    public async Task<string> ForAsync(IdentityUser user, CancellationToken ct)
    {
        var now = AthleteNow.Of(_clock);
        try
        {
            var today = await _facts.ReadAsync(user, now, ct);
            return TodayBriefing.Render(today with
            {
                Score = await ToolAsync(user, "GetTrainingScore", now.Today, ct),
                Next = await ToolAsync(user, "GetNextSessions", now.Today, ct),
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Today's briefing could not be read; the message goes with the clock alone.");
            return TodayBriefing.ClockOnly(now);
        }
    }

    /// <summary>
    /// The score, or what is up next, as its tool states it, run through the tool itself, so the
    /// briefing and the tool can never describe one day two ways, whatever shape the answer takes.
    /// </summary>
    private Task<string> ToolAsync(IdentityUser user, string tool, DateOnly today, CancellationToken ct) =>
        _tools.ExecuteToolCallAsync(
                new ToolInvocation("briefing", tool, JsonSerializer.Serialize(new { from = Iso(today), to = Iso(today) })),
                user, allowedToolNames: null, ct)
            .MatchAsync(
                answer => answer.Outcome.Content is "null" ? string.Empty : answer.Outcome.Content,
                problem =>
                {
                    _logger.LogWarning("The briefing could not read {Tool}: {Problem}", tool, problem.Description);
                    return string.Empty;
                });

    private static string Iso(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
