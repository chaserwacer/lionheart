using lionheart.Functional;
using lionheart.Model.Chat;
using lionheart.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Model.Chat.Completion;
using Services.Chat;

namespace lionheart.Tests.Llm;

/// <summary>
/// How a stored conversation is replayed to the model. The chat service switches over the
/// <see cref="LHChatMessage"/> hierarchy, and that switch ends in an <c>UnreachableException</c> —
/// so a new stored message type that nobody taught it about is a runtime crash on the chat path,
/// which is what the first test here exists to catch at build time instead.
/// </summary>
public sealed class StoredMessageReplayTests : IDisposable
{
    private readonly FakeChatServer _server = new();
    private readonly TestToolbox _toolbox = new();

    public void Dispose()
    {
        _server.Dispose();
        _toolbox.Dispose();
    }

    /// <summary>
    /// Adding a stored message type changes nothing until the replay switch handles it, because the
    /// discard arm absorbs it. This enumerates the hierarchy so the omission fails here.
    /// </summary>
    [Fact]
    public void Every_stored_message_type_is_either_replayed_or_the_system_prompt()
    {
        var declared = typeof(LHChatMessage).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(LHChatMessage)))
            .ToList();

        Assert.Equal(
            [
                typeof(LHChatToolCallResult),
                typeof(LHModelChatMessage),
                typeof(LHSystemChatMessage),
                typeof(LHUserChatMessage),
            ],
            declared.OrderBy(t => t.Name));
    }

    /// <summary>
    /// A conversation with one of each replayable type reaches the model as three turns, in
    /// chronological order, with the system prompt carried separately rather than among them.
    /// </summary>
    [Fact]
    public async Task A_full_history_is_replayed_in_order_with_the_system_prompt_first()
    {
        _server.Responds(CannedChat.Content("ok"));

        await Complete(WithFullHistory());

        var sent = Assert.Single(_server.ReceivedRequests);

        Assert.True(
            Position(sent, "You are a training assistant.") < Position(sent, "how did last week go?"),
            "The system prompt must precede the history.");
        Assert.True(
            Position(sent, "how did last week go?") < Position(sent, "you trained four times"),
            "A stored tool result must follow the user turn that led to it.");
        Assert.True(
            Position(sent, "you trained four times") < Position(sent, "You logged four sessions."),
            "The model's own answer must come last.");
    }

    /// <summary>
    /// A stored tool result is replayed as an assistant statement, never as a tool result: the
    /// request that asked for it is deliberately not stored, and a tool result whose matching
    /// request is absent is rejected by every provider.
    /// </summary>
    [Fact]
    public async Task A_stored_tool_result_is_replayed_without_claiming_to_answer_a_tool_call()
    {
        _server.Responds(CannedChat.Content("ok"));

        await Complete(WithFullHistory());

        var sent = Assert.Single(_server.ReceivedRequests);
        Assert.Contains("you trained four times", sent);
        Assert.DoesNotContain("\"role\":\"tool\"", sent);
        Assert.DoesNotContain("tool_call_id", sent);
    }

    private Task<Result<ChatCompletionResponse, Problem>> Complete(LHChatConversation conversation) =>
        new ChatCompletionService(_server.CreateChatModel(), _toolbox.Registry, _toolbox.Executor)
            .GenerateChatCompletionAsync(
                new IdentityUser("tester@lionheart.local"),
                new ChatCompletionRequest { Conversation = conversation, MaxOutputTokens = 1024 });

    /// <summary>One of every replayable stored type, in a deliberate chronological order.</summary>
    private static LHChatConversation WithFullHistory()
    {
        var conversation = AConversation.With("how did last week go?");
        var at = conversation.UserMessages[0].CreationTime;

        conversation.ToolMessages.Add(new LHChatToolCallResult
        {
            ChatMessageItemID = Guid.NewGuid(),
            ChatConversationID = conversation.ChatConversationID,
            CreationTime = at.AddSeconds(1),
            TokenCount = 8,
            Content = "you trained four times",
        });

        conversation.ModelMessages.Add(new LHModelChatMessage
        {
            ChatMessageItemID = Guid.NewGuid(),
            ChatConversationID = conversation.ChatConversationID,
            CreationTime = at.AddSeconds(2),
            TokenCount = 8,
            Content = "You logged four sessions.",
        });

        return conversation;
    }

    private static int Position(string body, string fragment)
    {
        var at = body.IndexOf(fragment, StringComparison.Ordinal);
        Assert.True(at >= 0, $"The request did not contain '{fragment}'. Body:\n{body}");
        return at;
    }
}
