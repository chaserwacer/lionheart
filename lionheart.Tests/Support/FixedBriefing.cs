using lionheart.Services.Chat.Today;
using Microsoft.AspNetCore.Identity;

namespace lionheart.Tests.Support;

/// <summary>A briefing that says the same thing every time, for tests about where a briefing goes rather than what it says.</summary>
public sealed class FixedBriefing(string text) : ITodayBriefing
{
    public string Text { get; set; } = text;

    public Task<string> ForAsync(IdentityUser user, CancellationToken ct) => Task.FromResult(Text);
}
