namespace lionheart.Model.Chat
{
    public record LHChatConversationDTO
    {
        public required Guid ChatConversationID { get; init; }
        public required DateTime CreatedAt { get; init; }
        public required DateTime LastUpdate { get; init; }
        public required string Name { get; init; }
        public required List<LHChatMessageDTO> Messages { get; init; }
    }

    /// <summary>
    /// Who produced a stored message, for a client that has to render the turn.
    /// </summary>
    /// <remarks>
    /// The stored hierarchy already distinguishes these as separate types, but a projection that
    /// drops the distinction leaves a client with no way to recover it: both tiers of the DTO carry
    /// the same members, so a conversation reads as an undifferentiated list of strings. Ordering
    /// does not stand in for it — the chronological projection is a merge of two tables, and any
    /// turn that does not come in a user/model pair makes every later turn read as the wrong
    /// speaker.
    /// </remarks>
    public enum LHChatMessageRole
    {
        User,
        Model,
        Tool,
        System
    }

    public class LHChatMessageDTO
    {
        public Guid ChatMessageItemID { get; init; }
        public Guid ChatConversationID { get; init; }
        public DateTime CreationTime { get; init; }
        public int TokenCount { get; init; }
        public string? Content { get; init; }
        public LHChatMessageRole Role { get; init; }

        public LHChatMessageDTO(LHChatMessage chatMessage)
        {
            ChatMessageItemID = chatMessage.ChatMessageItemID;
            ChatConversationID = chatMessage.ChatConversationID;
            CreationTime = chatMessage.CreationTime;
            TokenCount = chatMessage.TokenCount;
            Content = chatMessage.Content;
            Role = RoleOf(chatMessage);
        }

        /// <summary>
        /// The role a stored message's own type already states. Total over the hierarchy, and it
        /// throws on an unlisted subtype rather than defaulting: a new kind of stored message
        /// silently projected as a model turn would be rendered to the athlete as coaching advice.
        /// </summary>
        private static LHChatMessageRole RoleOf(LHChatMessage message) => message switch
        {
            LHUserChatMessage => LHChatMessageRole.User,
            LHModelChatMessage => LHChatMessageRole.Model,
            LHChatToolCallResult => LHChatMessageRole.Tool,
            LHSystemChatMessage => LHChatMessageRole.System,
            _ => throw new ArgumentOutOfRangeException(
                nameof(message), message.GetType().Name, "Unmapped stored chat message type.")
        };
    }

    public record CreateChatConversationRequest
    {
        public required string Name { get; init; }
    }
    public record UpdateChatConversationRequest
    {
        public required Guid ChatConversationID { get; init; }
        public required string Name { get; init; }
    }

    public record AddChatMessageRequest
    {
        public required Guid ChatConversationID { get; init; }
        public required string Content { get; init; }
    }

    public record StoreLHChatMessagesRequest
    {
        public required Guid ChatConversationID { get; init; }
        public required List<LHChatMessage> Messages { get; init; }
    }

    
}
