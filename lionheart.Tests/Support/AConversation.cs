using lionheart.Model.Chat;

namespace lionheart.Tests.Support;

/// <summary>Minimal conversation fixtures for driving <c>ChatCompletionService</c>.</summary>
public static class AConversation
{
    public static LHChatConversation With(string userMessage, int systemTokens = 50)
    {
        var conversationId = Guid.NewGuid();

        return new LHChatConversation
        {
            ChatConversationID = conversationId,
            UserID = Guid.NewGuid(),
            CreatedAt = Some.Clock,
            LastUpdate = Some.Clock,
            Name = "test conversation",
            ChatSystemMessage = new LHSystemChatMessage
            {
                ChatMessageItemID = Guid.NewGuid(),
                ChatConversationID = conversationId,
                CreationTime = Some.Clock,
                TokenCount = systemTokens,
                Content = "You are a training assistant.",
            },
            UserMessages =
            [
                new LHUserChatMessage
                {
                    ChatMessageItemID = Guid.NewGuid(),
                    ChatConversationID = conversationId,
                    CreationTime = Some.Clock.AddMinutes(1),
                    TokenCount = 10,
                    Content = userMessage,
                },
            ],
            ModelMessages = [],
            ToolMessages = [],
        };
    }
}
