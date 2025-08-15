using System.ComponentModel.DataAnnotations;
using OpenAI.Chat;

public class ChatConversationDTO
{
    [Required]
    public required Guid ChatConversationId { get; init; }
    [Required]
    public required DateTime CreatedAt { get; init; }
    [Required]
    public required string Name { get; set; } = string.Empty;
    [Required]
    public required List<ChatMessageItemDTO> Messages { get; set; } = new List<ChatMessageItemDTO>();

}

public class ChatMessageItemDTO
{
    [Required]
    public required Guid ChatMessageItemID { get; init; }
    [Required]
    public required Guid ChatConversationID { get; init; }
    [Required]
    public required ChatMessage ChatMessage { get; set; } = null!;
    [Required]
    public required DateTime CreationTime { get; set; } = DateTime.UtcNow;
    [Required]
    public required ChatMessageRole ChatMessageRole { get; set; }
    
}

public class CreateChatConversationRequest
{
    [Required]
    public required string Name { get; set; } = string.Empty;
}

public class UpdateChatConversationRequest
{
    [Required]
    public required Guid ChatConversationId { get; init; }
    [Required]
    public required string Name { get; set; } = string.Empty;
}

public class DeleteChatConversationRequest
{
    [Required]
    public required Guid ChatConversationId { get; init; }
}

public class AddChatMessageRequest
{
    [Required]
    public required Guid ChatConversationId { get; init; }
    [Required]
    public required ChatMessage ChatMessage { get; set; }
}