using OpenAI.Chat;

public record ToolCallResponse(ToolChatMessage ToolChatMessage, bool IsSuccess);