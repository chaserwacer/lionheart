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
    
    public class LHChatMessageDTO
    {
        public Guid ChatMessageItemID { get; init; }
        public Guid ChatConversationID { get; init; }
        public DateTime CreationTime { get; init; }
        public int TokenCount { get; init; }
        public string? Content { get; init; } 

        public LHChatMessageDTO(LHChatMessage chatMessage) 
        {
            ChatMessageItemID = chatMessage.ChatMessageItemID;
            ChatConversationID = chatMessage.ChatConversationID;
            CreationTime = chatMessage.CreationTime;
            TokenCount = chatMessage.TokenCount;
            Content = chatMessage.Content;
        }
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
