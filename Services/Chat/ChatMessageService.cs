using lionheart.Functional;
using lionheart.Data;
using lionheart.Model.Chat;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Model.Chat.Completion;
using Services.Chat;
using lionheart.Services.Chat.Today;

namespace lionheart.Services.Chat;

public interface IChatMessageService
{
    /// <summary>
    /// Intake user chat message, generate model response, store both + potential tool calls.
    /// Return model response message DTO.
    /// </summary>
    Task<Result<LHChatMessageDTO, Problem>> ProcessUserChatMessageAsync(IdentityUser user, AddChatMessageRequest request);
    /// <summary>
    /// Stores collection of chat messages in a conversation.
    /// Works with collection of varying LHChatMessage derived types.
    /// </summary>
    Task<Result<List<LHChatMessageDTO>, Problem>> StoreLHChatMessagesAsync(IdentityUser user, StoreLHChatMessagesRequest request);
    Task<Result<Unit, Problem>> DeleteChatMessageAsync(IdentityUser user, Guid chatMessageId);

}

public class ChatMessageService : IChatMessageService
{
    private readonly ModelContext _context;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly ITodayBriefing _briefing;

    public ChatMessageService(ModelContext context, IChatCompletionService chatCompletionService, ITodayBriefing briefing)
    {
        _context = context;
        _chatCompletionService = chatCompletionService;
        _briefing = briefing;
    }

    public async Task<Result<LHChatMessageDTO, Problem>> ProcessUserChatMessageAsync(IdentityUser user, AddChatMessageRequest request)
    {
        var userId = Guid.Parse(user.Id);

        var storedConversation = await _context.StandaloneConversations
        .Include(c => c.ChatSystemMessage)
                .Include(c => c.UserMessages)
                .Include(c => c.ModelMessages)
                .Include(c => c.ToolMessages)
            .FirstOrDefaultAsync(c => c.ChatConversationID == request.ChatConversationID && c.UserID == userId);

        if (storedConversation == null)
        {
            return Result<LHChatMessageDTO, Problem>.Failure(new Problem.NotFound("Conversation not found."));
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
            MaxOutputTokens = ChatCompletionRequest.ExchangeOutputTokens,
            Briefing = await _briefing.ForAsync(user, CancellationToken.None),
        };
        return await _chatCompletionService.GenerateChatCompletionAsync(user, chatCompletionRequest)
            .BindAsync(response => StoreLHChatMessagesAsync(user, new StoreLHChatMessagesRequest
                {
                    ChatConversationID = request.ChatConversationID,
                    Messages = response.CompletionGeneratedMessages
                })
                .MapAsync(_ => response))
            .BindAsync(response => CompleteExchangeAsync(storedConversation, response));
    }

    private async Task<Result<LHChatMessageDTO, Problem>> CompleteExchangeAsync(
        LHChatConversation conversation, ChatCompletionResponse response)
    {
        conversation.LastUpdate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result<LHChatMessageDTO, Problem>.Success(new LHChatMessageDTO(
            response.CompletionGeneratedMessages.First(
                m => m.ChatMessageItemID == response.ModelFinalChatMessageID)));
    }

    public async Task<Result<List<LHChatMessageDTO>, Problem>> StoreLHChatMessagesAsync(IdentityUser user, StoreLHChatMessagesRequest request)
    {
        var userId = Guid.Parse(user.Id);
        var conversation = await _context.StandaloneConversations
            .FirstOrDefaultAsync(c => c.ChatConversationID == request.ChatConversationID && c.UserID == userId);

        if (conversation == null)
        {
            return Result<List<LHChatMessageDTO>, Problem>.Failure(new Problem.NotFound("Conversation not found."));
        }

        _context.StageChatMessages(request.Messages);
        var storedMessages = request.Messages.Select(message => new LHChatMessageDTO(message)).ToList();

        conversation.LastUpdate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Result<List<LHChatMessageDTO>, Problem>.Success(storedMessages);
    }

    public async Task<Result<Unit, Problem>> DeleteChatMessageAsync(IdentityUser user, Guid chatMessageId)
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
                return Result<Unit, Problem>.Failure(new Problem.Forbidden("This message belongs to another user."));
            }
            _context.UserChatMessages.Remove(userMessage);
            await _context.SaveChangesAsync();
            return Result<Unit, Problem>.Success(Unit.Value);
        }

        var modelMessage = await _context.ModelChatMessages
            .Include(m => m.ChatConversation)
            .FirstOrDefaultAsync(m => m.ChatMessageItemID == chatMessageId);

        if (modelMessage != null)
        {
            if (modelMessage.ChatConversation.UserID != userId)
            {
                return Result<Unit, Problem>.Failure(new Problem.Forbidden("This message belongs to another user."));
            }
            _context.ModelChatMessages.Remove(modelMessage);
            await _context.SaveChangesAsync();
            return Result<Unit, Problem>.Success(Unit.Value);
        }

        var toolMessage = await _context.ToolChatMessages
            .Include(m => m.ChatConversation)
            .FirstOrDefaultAsync(m => m.ChatMessageItemID == chatMessageId);

        if (toolMessage != null)
        {
            if (toolMessage.ChatConversation.UserID != userId)
            {
                return Result<Unit, Problem>.Failure(new Problem.Forbidden("This message belongs to another user."));
            }
            _context.ToolChatMessages.Remove(toolMessage);
            await _context.SaveChangesAsync();
            return Result<Unit, Problem>.Success(Unit.Value);
        }

        return Result<Unit, Problem>.Failure(new Problem.NotFound("Message not found."));
    }

    private static int EstimateTokenCount(string? content)
    {
        // Simple estimation: 1 token ≈ 4 characters
        return (content?.Length ?? 0) / 4;
    }
}