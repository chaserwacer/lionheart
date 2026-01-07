using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using OpenAI.Chat;

/// <summary>
/// Represents a chat conversation that contain a collection of <see cref="ChatMessageItem"/>.
/// This entity corresponds to a chat session between a user and some AI model. 
/// </summary>
public class ChatConversation
{
    public Guid ChatConversationID { get; init; }
    public Guid UserID { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastUpdate { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<ChatMessageItem> Messages { get; set; } = new List<ChatMessageItem>();

}
/// <summary>
/// Represent a single message item within a <see cref="ChatConversation"/>.
/// </summary>
public class ChatMessageItem
{
    public Guid ChatMessageItemID { get; init; }
    public Guid ChatConversationID { get; init; }
    public ChatConversation ChatConversation { get; set; } = null!;
    public required string ChatMessageJson { get; set; } = string.Empty;
    public required ChatMessageRole ChatMessageRole { get; set; }
    public DateTime CreationTime { get; set; } = DateTime.UtcNow;   
}



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