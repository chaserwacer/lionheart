using System.ComponentModel.DataAnnotations.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;

public class ChatConversation
{
    public Guid ChatConversationId { get; init; }
    public Guid UserID { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string Name { get; set; } = string.Empty;
    public List<ChatMessageItem> Messages { get; set; } = new List<ChatMessageItem>();

    public ChatConversationDTO ToDTO()
    {
        return new ChatConversationDTO
        {
            ChatConversationId = this.ChatConversationId,
            CreatedAt = this.CreatedAt,
            Name = this.Name,
            Messages = this.Messages.Select(m => m.ToDTO()).ToList().OrderBy(m => m.CreationTime).ToList()
        };
    }
}

public class ChatMessageItem
{
    public Guid ChatMessageItemID { get; init; }
    public Guid ChatConversationID { get; init; }
    public ChatConversation ChatConversation { get; set; } = null!;
    public required string ChatMessageJson { get; set; } = string.Empty;
    public required ChatMessageRole ChatMessageRole { get; set; }

    private ChatMessage DeserializeChatMessage(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            // Fallback to empty minimal message per role
            return ChatMessageRole switch
            {
                ChatMessageRole.System => new SystemChatMessage(string.Empty),
                ChatMessageRole.User => new UserChatMessage(string.Empty),
                ChatMessageRole.Assistant => new AssistantChatMessage(string.Empty),
                ChatMessageRole.Tool => new ToolChatMessage(string.Empty, string.Empty),
                _ => new AssistantChatMessage(string.Empty)
            };
        }

        try
        {
            var jo = JObject.Parse(json);

            // Extract text content from multiple possible shapes
            string? text = null;
            string? toolCallId = null;

            // Minimal shape: { content: "..." }
            text = jo["content"]?.Type == JTokenType.String ? (string?)jo["content"] : text;
            text ??= jo["Content"]?.Type == JTokenType.String ? (string?)jo["Content"] : null;

            // Array-of-parts shape: { content: [ { text: "..." } ] }
            if (text is null)
            {
                var parts = jo["content"] as JArray ?? jo["Content"] as JArray;
                var firstPart = parts != null && parts.Count > 0 ? parts[0] as JObject : null;
                text = firstPart?["text"]?.ToString() ?? firstPart?["Text"]?.ToString();
            }

            // ToolCallId if present
            toolCallId = jo["toolCallId"]?.ToString() ?? jo["ToolCallId"]?.ToString();

            // Construct concrete message from role
            return ChatMessageRole switch
            {
                ChatMessageRole.System => new SystemChatMessage(text ?? string.Empty),
                ChatMessageRole.User => new UserChatMessage(text ?? string.Empty),
                ChatMessageRole.Assistant => new AssistantChatMessage(text ?? string.Empty),
                ChatMessageRole.Tool => new ToolChatMessage(toolCallId ?? string.Empty, text ?? string.Empty),
                _ => new AssistantChatMessage(text ?? string.Empty)
            };
        }
        catch
        {
            // Any failure: return an empty safe message to avoid null content
            return ChatMessageRole switch
            {
                ChatMessageRole.System => new SystemChatMessage(string.Empty),
                ChatMessageRole.User => new UserChatMessage(string.Empty),
                ChatMessageRole.Assistant => new AssistantChatMessage(string.Empty),
                ChatMessageRole.Tool => new ToolChatMessage(string.Empty, string.Empty),
                _ => new AssistantChatMessage(string.Empty)
            };
        }
    }

    [NotMapped]
    public ChatMessage ChatMessage
    {
        get => DeserializeChatMessage(ChatMessageJson);
        set
        {
            if (value is null)
            {
                ChatMessageJson = string.Empty;
                return;
            }

            // Store a minimal, SDK-agnostic shape
            string role = ChatMessageRole switch
            {
                ChatMessageRole.System => "system",
                ChatMessageRole.User => "user",
                ChatMessageRole.Assistant => "assistant",
                ChatMessageRole.Tool => "tool",
                _ => "assistant"
            };

            string? text = null;
            try
            {
                // Try to extract first text part
                if (value.Content is not null && value.Content.Count > 0)
                {
                    var part = value.Content[0];
                    // dynamic for safety across SDK changes
                    text = (part?.Text) as string;
                }
            }
            catch { /* ignore extraction issues */ }

            string? toolCallId = null;
            if (value is ToolChatMessage toolMsg)
            {
                try
                {
                    // Attempt to read via reflection if property exists
                    var prop = typeof(ToolChatMessage).GetProperty("ToolCallId");
                    toolCallId = prop?.GetValue(toolMsg)?.ToString();
                }
                catch { /* ignore */ }
            }

            var stored = new JObject
            {
                ["role"] = role,
                ["content"] = text ?? string.Empty
            };
            if (!string.IsNullOrWhiteSpace(toolCallId))
            {
                stored["toolCallId"] = toolCallId;
            }

            ChatMessageJson = stored.ToString(Formatting.None);
        }
    }

    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
    public ChatMessageItemDTO ToDTO()
    {
        var chatMessageRole = ChatMessage switch
        {
            SystemChatMessage => ChatMessageRole.System,
            UserChatMessage => ChatMessageRole.User,
            AssistantChatMessage => ChatMessageRole.Assistant,
            ToolChatMessage => ChatMessageRole.Tool,
            _ => ChatMessageRole.Assistant
        };
        return new ChatMessageItemDTO
            {
                ChatMessageItemID = this.ChatMessageItemID,
                ChatConversationID = this.ChatConversationID,
                ChatMessage = this.ChatMessage,
                CreationTime = this.CreationTime,
                ChatMessageRole = chatMessageRole
            };
    }
}

// Derived types used as EF Core TPH discriminator values
public class SystemChatMessageItem : ChatMessageItem { }
public class UserChatMessageItem : ChatMessageItem { }
public class AssistantChatMessageItem : ChatMessageItem { }
public class ToolChatMessageItem : ChatMessageItem { }