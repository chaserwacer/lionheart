using OpenAI.Chat;

public record ToolCallResponse(ToolChatMessage ToolChatMessage, bool IsSuccess);
/// <summary>
/// Store a <see cref="ToolCallResponse"/>, as well as the object the tool call retreived/returned. 
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="ToolCallResponse"></param>
/// <param name="Value"></param>
public record ToolCallExecutorResponse<T>(ToolCallResponse ToolCallResponse,T Value);
