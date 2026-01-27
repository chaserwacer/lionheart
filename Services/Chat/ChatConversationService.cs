

using Ardalis.Result;
using lionheart.Data;
using lionheart.Model.Chat;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Chat
{


    public interface IChatConversationService
    {
        Task<Result<LHChatConversationDTO>> CreateChatConversationAsync(IdentityUser user, CreateChatConversationRequest request);
        Task<Result<LHChatConversationDTO>> GetChatConversationAsync(IdentityUser user, Guid conversationId);
        Task<Result<List<LHChatConversationDTO>>> GetAllChatConversationsAsync(IdentityUser user);
        Task<Result<LHChatConversationDTO>> GetMostRecentChatConversationAsync(IdentityUser user);
        Task<Result<LHChatConversationDTO>> UpdateChatConversationAsync(IdentityUser user, UpdateChatConversationRequest request);
        Task<Result> DeleteChatConversationAsync(IdentityUser user, Guid chatConversationId);
    }
    public class ChatConversationService : IChatConversationService
    {
        private readonly ModelContext _context;

        public ChatConversationService(ModelContext context)
        {
            _context = context;
        }

        public async Task<Result<LHChatConversationDTO>> CreateChatConversationAsync(IdentityUser user, CreateChatConversationRequest request)
        {
            var userId = Guid.Parse(user.Id);
            var conversationId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            var systemMessage = new LHSystemChatMessage
            {
                ChatMessageItemID = Guid.NewGuid(),
                ChatConversationID = conversationId,
                CreationTime = now,
                TokenCount = 200, 
                Content = """
                You are Lionheart, an intelligent training coach and analyst.
                You access the Lionheart Training Intelligence System: an athlete's training history, subjective notes, and wearable biometrics (e.g., Oura Ring).
                Core principles:
                Interpret, don't report. Never restate raw data or list metrics.
                Prioritize patterns and trends over snapshots. Reference numbers only when they strengthen insight.
                Analyze in context: load vs. recovery, performance vs. fatigue, lifestyle stress alongside training.
                Use tools proactively to retrieve only what's needed. Never expose tool usage.
                Tone: Thoughtful coach—intelligent, grounded, human. Engaging, not robotic.
                Always aim to add value through insights and actionable advice.
                You are a training intelligence layer, not a dashboard.
                """
            };

            var conversation = new LHChatConversation
            {
                ChatConversationID = conversationId,
                UserID = userId,
                CreatedAt = now,
                LastUpdate = now,
                Name = request.Name,
                ChatSystemMessage = systemMessage,
                ModelMessages = new List<LHModelChatMessage>(),
                UserMessages = new List<LHUserChatMessage>(),
                ToolMessages = new List<LHChatToolCallResult>()
            };

            _context.ChatConversations.Add(conversation);
            await _context.SaveChangesAsync();

            return Result<LHChatConversationDTO>.Created(conversation.ToDTO());
        }

        public async Task<Result> DeleteChatConversationAsync(IdentityUser user, Guid chatConversationId)
        {
            var userId = Guid.Parse(user.Id);
            var conversation = await _context.ChatConversations
                .FirstOrDefaultAsync(c => c.ChatConversationID == chatConversationId && c.UserID == userId);

            if (conversation == null)
            {
                return Result.NotFound("Conversation not found.");
            }

            _context.ChatConversations.Remove(conversation);
            await _context.SaveChangesAsync();

            return Result.Success();
        }

        public async Task<Result<List<LHChatConversationDTO>>> GetAllChatConversationsAsync(IdentityUser user)
        {
            var userId = Guid.Parse(user.Id);
            var conversations = await _context.ChatConversations
                .Where(c => c.UserID == userId)
                .OrderByDescending(c => c.LastUpdate)
                .ProjectToType<LHChatConversationDTO>()
                .ToListAsync();
            return Result.Success(conversations);
            
        }

        public async Task<Result<LHChatConversationDTO>> GetChatConversationAsync(IdentityUser user, Guid conversationId)
        {
            var userId = Guid.Parse(user.Id);
            var conversation = await _context.ChatConversations
                .Include(c => c.ChatSystemMessage)
                .Include(c => c.UserMessages)
                .Include(c => c.ModelMessages)
                .Include(c => c.ToolMessages)
                .FirstOrDefaultAsync(c => c.ChatConversationID == conversationId && c.UserID == userId);

            if (conversation == null)
            {
                return Result.NotFound("Conversation not found.");
            }

            return Result.Success(conversation.ToDTO());
        }

        public async Task<Result<LHChatConversationDTO>> GetMostRecentChatConversationAsync(IdentityUser user)
        {
            var userId = Guid.Parse(user.Id);
            var conversation = await _context.ChatConversations
                .Where(c => c.UserID == userId)
                .OrderByDescending(c => c.LastUpdate)
                .Include(c => c.UserMessages)
                .Include(c => c.ModelMessages)
                .FirstOrDefaultAsync();
            if (conversation == null)
            {
                return Result.NotFound("No conversations found.");
            }
            return Result.Success(conversation.ToDTO());


        }

        public async Task<Result<LHChatConversationDTO>> UpdateChatConversationAsync(IdentityUser user, UpdateChatConversationRequest request)
        {
            var userId = Guid.Parse(user.Id);
            var conversation = await _context.ChatConversations
                .FirstOrDefaultAsync(c => c.ChatConversationID == request.ChatConversationID && c.UserID == userId);

            if (conversation == null)
            {
                return Result.NotFound("Conversation not found.");
            }

            conversation.Name = request.Name;
            conversation.LastUpdate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Result.Success(conversation.ToDTO());
        }

        
    }
}