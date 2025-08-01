using Ardalis.Result;
using Microsoft.AspNetCore.Identity;
using OpenAI.Chat;

public interface IToolCallExecutor
{
    Task<List<Result<ToolChatMessage>>> ExecuteToolCallsAsync(IReadOnlyList<ChatToolCall> toolCalls, IdentityUser user);
    Task<List<ToolCallResponse>> ExecuteModifyTrainingSessionToolCallsAsync(IReadOnlyList<ChatToolCall> toolCalls, IdentityUser user);
    Task<List<ToolCallResponse>> ExecuteChatToolCallsAsync(IReadOnlyList<ChatToolCall> toolCalls, IdentityUser user);
}