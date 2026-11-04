using System.Text.Json;
using lionheart.Services.Llm;
using lionheart.Tests.Support;

namespace lionheart.Tests.Llm;

/// <summary>
/// Where each provider puts a round's briefing. On Anthropic it has to follow the athlete's latest
/// words, after the cache breakpoint on them: anywhere earlier and the prefix the next message is
/// read from would never match what was cached.
/// </summary>
public sealed class BriefingPlacementTests : IDisposable
{
    private const string Briefing = "TODAY\nNow: Wednesday 4 November 2026, 07:12 (-05:00).";

    private readonly FakeAnthropicServer _anthropic = new();
    private readonly FakeChatServer _lmStudio = new();

    public void Dispose()
    {
        _anthropic.Dispose();
        _lmStudio.Dispose();
    }

    private static ChatTurnRequest Conversation(params ModelMessage[] messages) =>
        ChatTurnRequest.Conversation("You are Lionheart.", messages, [], 1024) with { Briefing = Briefing };

    private JsonElement SentToAnthropic(int index = 0) => JsonDocument.Parse(_anthropic.ReceivedRequests[index]).RootElement;

    [Fact]
    public async Task On_anthropic_the_briefing_is_a_system_message_after_the_cached_latest_words()
    {
        _anthropic.Responds(CannedAnthropic.Text("Train."));

        await _anthropic.CreateChatModel().RespondAsync(Conversation(
            new ModelMessage.UserSaid("how was last week?"),
            new ModelMessage.AssistantSaid("Solid."),
            new ModelMessage.UserSaid("should I train today?")));

        var messages = SentToAnthropic().GetProperty("messages");
        Assert.Equal(4, messages.GetArrayLength());
        Assert.Equal("user", messages[2].GetProperty("role").GetString());
        Assert.True(messages[2].GetProperty("content")[0].TryGetProperty("cache_control", out _));
        Assert.Equal("system", messages[3].GetProperty("role").GetString());
        Assert.Equal(Briefing, messages[3].GetProperty("content").GetString());
        Assert.DoesNotContain("Wednesday", SentToAnthropic().GetProperty("system").ToString());
    }

    /// <summary>The exchange is append-only: a tool round adds after the briefing and never moves it.</summary>
    [Fact]
    public async Task On_anthropic_a_tool_round_keeps_the_briefing_where_the_first_round_put_it()
    {
        _anthropic.Responds(CannedAnthropic.ToolUse("GetTrainingScore", "{}"));
        _anthropic.Responds(CannedAnthropic.Text("Train."));
        var model = _anthropic.CreateChatModel();

        var first = await model.RespondAsync(Conversation(new ModelMessage.UserSaid("should I train today?")));
        var requested = Assert.IsType<ModelTurn.RequestedTools>(ResultAssert.Succeeded(first));
        await model.RespondAsync(Conversation(
            new ModelMessage.UserSaid("should I train today?"),
            new ModelMessage.AssistantActed(requested.Turn),
            new ModelMessage.ToolsAnswered([ToolOutcome.Produced("toolu_1", "71 of 100")])));

        var roles = SentToAnthropic(1).GetProperty("messages").EnumerateArray()
            .Select(message => message.GetProperty("role").GetString()).ToList();
        Assert.Equal(["user", "system", "assistant", "user"], roles);
    }

    [Fact]
    public async Task On_anthropic_no_briefing_sends_no_system_message()
    {
        _anthropic.Responds(CannedAnthropic.Text("Train."));

        await _anthropic.CreateChatModel().RespondAsync(
            Conversation(new ModelMessage.UserSaid("should I train today?")) with { Briefing = string.Empty });

        var message = Assert.Single(SentToAnthropic().GetProperty("messages").EnumerateArray());
        Assert.Equal("user", message.GetProperty("role").GetString());
    }

    /// <summary>Local chat templates refuse a system message anywhere but first.</summary>
    [Fact]
    public async Task On_lm_studio_the_briefing_follows_the_system_prompt_in_the_first_message()
    {
        _lmStudio.Responds(CannedChat.Content("Train."));

        await _lmStudio.CreateChatModel().RespondAsync(Conversation(new ModelMessage.UserSaid("should I train today?")));

        var messages = JsonDocument.Parse(_lmStudio.ReceivedRequests[0]).RootElement.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal($"You are Lionheart.\n\n{Briefing}", messages[0].GetProperty("content").GetString());
        Assert.Equal(1, messages.EnumerateArray().Count(message => message.GetProperty("role").GetString() == "system"));
    }
}
