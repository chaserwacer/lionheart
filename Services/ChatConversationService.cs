using System.ComponentModel;
using Newtonsoft.Json;
using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using OllamaSharp;
using OpenAI.Chat;

namespace lionheart.Services;

[McpServerToolType]
public class ChatConversationService : IChatConversationService
{
    private readonly ModelContext _context;

    public ChatConversationService(ModelContext context)
    {
        _context = context;
    }

    [McpServerTool, Description("Create a new chat conversation.")]
    public async Task<Result<ChatConversationDTO>> CreateChatConversationAsync(IdentityUser user, CreateChatConversationRequest request)
    {
        var userGuid = Guid.Parse(user.Id);

        // Verify user exists
        var lionheartUser = await _context.LionheartUsers
            .FirstOrDefaultAsync(u => u.UserID == userGuid);

        if (lionheartUser is null)
        {
            return Result<ChatConversationDTO>.NotFound("User not found.");
        }

        var chatConversation = new ChatConversation
        {
            ChatConversationId = Guid.NewGuid(),
            UserID = userGuid,
            Name = request.Name,
            CreatedAt = DateTime.UtcNow,
            Messages = new List<ChatMessageItem>()
        };

        await _context.ChatConversations.AddAsync(chatConversation);
        await _context.SaveChangesAsync();

        return Result<ChatConversationDTO>.Success(chatConversation.ToDTO());
    }

    [McpServerTool, Description("Get a chat conversation by ID.")]
    public async Task<Result<ChatConversationDTO>> GetChatConversationAsync(IdentityUser user, Guid conversationId)
    {
        var userGuid = Guid.Parse(user.Id);

        var chatConversation = await _context.ChatConversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.ChatConversationId == conversationId && c.UserID == userGuid);

        if (chatConversation is null)
        {
            return Result<ChatConversationDTO>.NotFound("Chat conversation not found or access denied.");
        }

        return Result<ChatConversationDTO>.Success(chatConversation.ToDTO());
    }

    [McpServerTool, Description("Get a chat conversation by ID.")]
    public async Task<Result<ChatConversation>> GetChatConversationModelVersionAsync(IdentityUser user, Guid conversationId)
    {
        var userGuid = Guid.Parse(user.Id);

        var chatConversation = await _context.ChatConversations
            .Where(c => c.UserID == userGuid && c.ChatConversationId == conversationId)
            .Include(c => c.Messages)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (chatConversation is null)
        {
            return Result<ChatConversation>.NotFound("Chat conversation not found or access denied.");
        }

        return Result<ChatConversation>.Success(chatConversation);
    }

    [McpServerTool, Description("Get all chat conversations for a user.")]
    public async Task<Result<List<ChatConversationDTO>>> GetAllChatConversationsAsync(IdentityUser user)
    {
        var userGuid = Guid.Parse(user.Id);

        var chatConversations = await _context.ChatConversations
            .Where(c => c.UserID == userGuid)
            .Include(c => c.Messages)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return Result<List<ChatConversationDTO>>.Success(chatConversations.Select(c => c.ToDTO()).ToList());
    }

    [McpServerTool, Description("Update an existing chat conversation.")]
    public async Task<Result<ChatConversationDTO>> UpdateChatConversationAsync(IdentityUser user, UpdateChatConversationRequest request)
    {
        var userGuid = Guid.Parse(user.Id);

        var chatConversation = await _context.ChatConversations
            .FirstOrDefaultAsync(c => c.ChatConversationId == request.ChatConversationId && c.UserID == userGuid);

        if (chatConversation is null)
        {
            return Result<ChatConversationDTO>.NotFound("Chat conversation not found or access denied.");
        }

        chatConversation.Name = request.Name;
        await _context.SaveChangesAsync();

        return Result<ChatConversationDTO>.Success(chatConversation.ToDTO());
    }

    [McpServerTool, Description("Delete a chat conversation.")]
    public async Task<Result> DeleteChatConversationAsync(IdentityUser user, DeleteChatConversationRequest request)
    {
        var userGuid = Guid.Parse(user.Id);

        var chatConversation = await _context.ChatConversations
            .FirstOrDefaultAsync(c => c.ChatConversationId == request.ChatConversationId && c.UserID == userGuid);

        if (chatConversation is null)
        {
            return Result.NotFound("Chat conversation not found or access denied.");
        }

        _context.ChatConversations.Remove(chatConversation);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    [McpServerTool, Description("Add a message to an existing chat conversation.")]
    public async Task<Result<ChatConversationDTO>> AddChatMessageAsync(IdentityUser user, AddChatMessageRequest request)
    {
        var userGuid = Guid.Parse(user.Id);

        var chatConversation = await _context.ChatConversations
            .FirstOrDefaultAsync(c => c.ChatConversationId == request.ChatConversationId && c.UserID == userGuid);

        if (chatConversation is null)
        {
            return Result<ChatConversationDTO>.NotFound("Chat conversation not found or access denied.");
        }
        var chatMessageRole = request.ChatMessage switch
        {
            SystemChatMessage => ChatMessageRole.System,
            UserChatMessage => ChatMessageRole.User,
            AssistantChatMessage => ChatMessageRole.Assistant,
            ToolChatMessage => ChatMessageRole.Tool,
            _ => ChatMessageRole.Assistant
        };
        var chatMessageItem = new ChatMessageItem
        {
            ChatMessageItemID = Guid.NewGuid(),
            ChatConversationID = chatConversation.ChatConversationId,
            ChatConversation = chatConversation,
            // Serialize with Newtonsoft to match deserialization logic
            ChatMessageJson = JsonConvert.SerializeObject(request.ChatMessage),
            CreationTime = DateTime.UtcNow,
            ChatMessageRole = chatMessageRole

        };

        

        await _context.ChatMessageItems.AddAsync(chatMessageItem);
        await _context.SaveChangesAsync();

        return Result<ChatConversationDTO>.Success(chatConversation.ToDTO());
    }
}