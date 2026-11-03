using lionheart.Model.Chat;
using lionheart.Model.Chat.Thread;
using lionheart.Tests.Support;

namespace lionheart.Tests.ChatThread;

/// <summary>
/// Where one session of the thread ends and the next begins: an idle gap, or a transcript grown
/// full. A message inside both limits belongs to the session already open.
/// </summary>
public sealed class SessionBoundaryTests
{
    private static readonly DateTime LastSpoke = Some.Clock;

    private static SessionTip Tip(int transcriptCharacters = 400)
    {
        var conversation = AConversation.With(new string('x', transcriptCharacters));
        conversation.LastUpdate = LastSpoke;
        return new SessionTip(
            ChatThreadSession.Open(conversation.ChatConversationID, conversation.UserID, LastSpoke.AddMinutes(-20)),
            conversation);
    }

    [Fact]
    public void The_first_message_to_the_thread_opens_its_first_session() =>
        Assert.IsType<SessionBoundary.NoSessionYet>(SessionBoundary.Decide(null, LastSpoke));

    [Theory]
    [InlineData(5)]
    [InlineData(40)]
    [InlineData(179)]
    public void Coming_back_within_the_gap_continues_the_session(int minutesLater)
    {
        var tip = Tip();

        var boundary = SessionBoundary.Decide(tip, LastSpoke.AddMinutes(minutesLater));

        Assert.Same(tip, Assert.IsType<SessionBoundary.Continues>(boundary).Tip);
    }

    [Theory]
    [InlineData(180)]
    [InlineData(300)]
    [InlineData(720)]
    public void Coming_back_after_the_gap_ends_the_session(int minutesLater)
    {
        var boundary = SessionBoundary.Decide(Tip(), LastSpoke.AddMinutes(minutesLater));

        Assert.Equal(SessionEnding.Idle, Assert.IsType<SessionBoundary.Ended>(boundary).Why);
    }

    [Fact]
    public void A_session_that_has_grown_full_ends_even_without_a_gap()
    {
        var full = Tip(transcriptCharacters: ChatSessionPolicy.MaxTranscriptTokens * 4);

        var boundary = SessionBoundary.Decide(full, LastSpoke.AddMinutes(1));

        Assert.Equal(SessionEnding.Full, Assert.IsType<SessionBoundary.Ended>(boundary).Why);
    }

    [Fact]
    public void Tool_results_count_toward_a_session_being_full_because_they_are_resent_too()
    {
        var tip = Tip(transcriptCharacters: 4);
        tip.Conversation.ToolMessages.Add(new LHChatToolCallResult
        {
            ChatMessageItemID = Guid.NewGuid(),
            ChatConversationID = tip.Conversation.ChatConversationID,
            CreationTime = LastSpoke,
            TokenCount = 0,
            Content = new string('y', ChatSessionPolicy.MaxTranscriptTokens * 4),
        });

        Assert.IsType<SessionBoundary.Ended>(SessionBoundary.Decide(tip, LastSpoke.AddMinutes(1)));
    }
}
