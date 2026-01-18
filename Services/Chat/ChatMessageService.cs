using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Chat;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Model.Chat.Completion;
using Model.Chat.Tools;
using OpenAI.Chat;
using Services.Chat;

namespace lionheart.Services.Chat;

public interface IChatMessageService
{
    /// <summary>
    /// Intake user chat message, generate model response, store both + potential tool calls.
    /// Return model response message DTO.
    /// </summary>
    Task<Result<LHChatMessageDTO>> ProcessUserChatMessageAsync(IdentityUser user, AddChatMessageRequest request);
    /// <summary>
    /// Stores collection of chat messages in a conversation.
    /// Works with collection of varying LHChatMessage derived types.
    /// </summary>
    Task<Result<List<LHChatMessageDTO>>> StoreLHChatMessagesAsync(IdentityUser user, StoreLHChatMessagesRequest request);
    Task<Result> DeleteChatMessageAsync(IdentityUser user, Guid chatMessageId);

}

public class ChatMessageService : IChatMessageService
{
    private readonly ModelContext _context;
    private readonly ChatCompletionService _chatCompletionService;
    private readonly ToolRegistry _toolRegistry;

    public ChatMessageService(ModelContext context, ChatCompletionService chatCompletionService, ToolRegistry toolRegistry)
    {
        _context = context;
        _chatCompletionService = chatCompletionService;
        _toolRegistry = toolRegistry;
    }

    public async Task<Result<LHChatMessageDTO>> ProcessUserChatMessageAsync(IdentityUser user, AddChatMessageRequest request)
    {
        var userId = Guid.Parse(user.Id);

        var storedConversation = await _context.ChatConversations
            .FirstOrDefaultAsync(c => c.ChatConversationID == request.ChatConversationID && c.UserID == userId);

        if (storedConversation == null)
        {
            return Result.NotFound("Conversation not found.");
        }

        var message = new LHUserChatMessage
        {
            ChatMessageItemID = Guid.NewGuid(),
            ChatConversationID = request.ChatConversationID,
            CreationTime = DateTime.UtcNow,
            TokenCount = EstimateTokenCount(request.Content),
            Content = request.Content
        };

        _context.UserChatMessages.Add(message);

        var chatCompletionRequest = new ChatCompletionRequest
        {
            Conversation = storedConversation,
            Options = GetChatCompletionOptions(_toolRegistry.GetAllChatTools())
        };
        var responseMessage = await _chatCompletionService.GenerateChatCompletionAsync(user, chatCompletionRequest);

        if (responseMessage.IsError())
        {
            return Result.Error(responseMessage.Errors.ToString());
        }
        var storageResult = await StoreLHChatMessagesAsync(user, new StoreLHChatMessagesRequest
        {
            ChatConversationID = request.ChatConversationID,
            Messages = responseMessage.Value.CompletionGeneratedMessages
        }); 
        if (storageResult.IsError())
        {
            return Result.Error(storageResult.Errors.ToString());
        }

        storedConversation.LastUpdate = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Result.Success(new LHChatMessageDTO(responseMessage.Value.CompletionGeneratedMessages.Select(m => m).First(m => m.ChatMessageItemID == responseMessage.Value.ModelFinalChatMessageID)));
    }
    private ChatCompletionOptions GetChatCompletionOptions(IEnumerable<ChatTool> tools)
    {
        var completionOptions = new ChatCompletionOptions();
        foreach (var tool in tools)
        {
            completionOptions.Tools.Add(tool);
        }

        return completionOptions;
    }

    public async Task<Result<List<LHChatMessageDTO>>> StoreLHChatMessagesAsync(IdentityUser user, StoreLHChatMessagesRequest request)
    {
        var userId = Guid.Parse(user.Id);
        var conversation = await _context.ChatConversations
    .FirstOrDefaultAsync(c => c.ChatConversationID == request.ChatConversationID && c.UserID == userId);

        if (conversation == null)
        {
            return Result.NotFound("Conversation not found.");
        }

        var storedMessages = new List<LHChatMessageDTO>();

        foreach (var message in request.Messages)
        {
            switch (message)
            {
                case LHUserChatMessage userMessage:
                    _context.UserChatMessages.Add(userMessage);
                    storedMessages.Add(new LHChatMessageDTO(userMessage));
                    break;
                case LHModelChatMessage modelMessage:
                    _context.ModelChatMessages.Add(modelMessage);
                    storedMessages.Add(new LHChatMessageDTO(modelMessage));
                    break;
                case LHToolChatMessage toolMessage:
                    _context.ToolChatMessages.Add(toolMessage);
                    storedMessages.Add(new LHChatMessageDTO(toolMessage));
                    break;
                case LHSystemChatMessage systemMessage:
                    _context.SystemChatMessages.Add(systemMessage);
                    storedMessages.Add(new LHChatMessageDTO(systemMessage));
                    break;
            }
        }

        conversation.LastUpdate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Result.Success(storedMessages);
    }

    public async Task<Result> DeleteChatMessageAsync(IdentityUser user, Guid chatMessageId)
    {
        var userId = Guid.Parse(user.Id);

        // Try to find the message in each table
        var userMessage = await _context.UserChatMessages
            .Include(m => m.ChatConversation)
            .FirstOrDefaultAsync(m => m.ChatMessageItemID == chatMessageId);

        if (userMessage != null)
        {
            if (userMessage.ChatConversation.UserID != userId)
            {
                return Result.Forbidden();
            }
            _context.UserChatMessages.Remove(userMessage);
            await _context.SaveChangesAsync();
            return Result.Success();
        }

        var modelMessage = await _context.ModelChatMessages
            .Include(m => m.ChatConversation)
            .FirstOrDefaultAsync(m => m.ChatMessageItemID == chatMessageId);

        if (modelMessage != null)
        {
            if (modelMessage.ChatConversation.UserID != userId)
            {
                return Result.Forbidden();
            }
            _context.ModelChatMessages.Remove(modelMessage);
            await _context.SaveChangesAsync();
            return Result.Success();
        }

        var toolMessage = await _context.ToolChatMessages
            .Include(m => m.ChatConversation)
            .FirstOrDefaultAsync(m => m.ChatMessageItemID == chatMessageId);

        if (toolMessage != null)
        {
            if (toolMessage.ChatConversation.UserID != userId)
            {
                return Result.Forbidden();
            }
            _context.ToolChatMessages.Remove(toolMessage);
            await _context.SaveChangesAsync();
            return Result.Success();
        }

        return Result.NotFound("Message not found.");
    }

    private static int EstimateTokenCount(string? content)
    {
        // Simple estimation: 1 token ≈ 4 characters
        return (content?.Length ?? 0) / 4;
    }
}