

using lionheart.Functional;
using lionheart.Data;
using lionheart.Model.Chat;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace lionheart.Services.Chat
{


    public interface IChatConversationService
    {
        Task<Result<LHChatConversationDTO, Problem>> CreateChatConversationAsync(IdentityUser user, CreateChatConversationRequest request);
        Task<Result<LHChatConversationDTO, Problem>> GetChatConversationAsync(IdentityUser user, Guid conversationId);
        Task<Result<List<LHChatConversationDTO>, Problem>> GetAllChatConversationsAsync(IdentityUser user);
        Task<Result<LHChatConversationDTO, Problem>> GetMostRecentChatConversationAsync(IdentityUser user);
        Task<Result<LHChatConversationDTO, Problem>> UpdateChatConversationAsync(IdentityUser user, UpdateChatConversationRequest request);
        Task<Result<Unit, Problem>> DeleteChatConversationAsync(IdentityUser user, Guid chatConversationId);
    }
    public class ChatConversationService : IChatConversationService
    {
        private readonly ModelContext _context;
        private readonly ChatSystemPrompt _systemPrompt;

        public ChatConversationService(ModelContext context, ChatSystemPrompt systemPrompt)
        {
            _context = context;
            _systemPrompt = systemPrompt;
        }

        public async Task<Result<LHChatConversationDTO, Problem>> CreateChatConversationAsync(IdentityUser user, CreateChatConversationRequest request)
        {
            var userId = Guid.Parse(user.Id);
            var conversationId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            // The card goes into the system message so the model starts with holistic context and a
            // tool-routing policy.
            var systemContent = await _systemPrompt.ForAsync(user);

            var systemMessage = new LHSystemChatMessage
            {
                ChatMessageItemID = Guid.NewGuid(),
                ChatConversationID = conversationId,
                CreationTime = now,
                TokenCount = systemContent.Length / 4,
                Content = systemContent
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

            return Result<LHChatConversationDTO, Problem>.Success(conversation.ToDTO());
        }

        public async Task<Result<Unit, Problem>> DeleteChatConversationAsync(IdentityUser user, Guid chatConversationId)
        {
            var userId = Guid.Parse(user.Id);
            var conversation = await _context.StandaloneConversations
                .FirstOrDefaultAsync(c => c.ChatConversationID == chatConversationId && c.UserID == userId);

            if (conversation == null)
            {
                return Result<Unit, Problem>.Failure(new Problem.NotFound("Conversation not found."));
            }

            _context.ChatConversations.Remove(conversation);
            await _context.SaveChangesAsync();

            return Result<Unit, Problem>.Success(Unit.Value);
        }

        public async Task<Result<List<LHChatConversationDTO>, Problem>> GetAllChatConversationsAsync(IdentityUser user)
        {
            var userId = Guid.Parse(user.Id);
            var conversations = await _context.StandaloneConversations
                .Where(c => c.UserID == userId)
                .OrderByDescending(c => c.LastUpdate)
                .ProjectToType<LHChatConversationDTO>()
                .ToListAsync();
            return Result<List<LHChatConversationDTO>, Problem>.Success(conversations);
            
        }

        public async Task<Result<LHChatConversationDTO, Problem>> GetChatConversationAsync(IdentityUser user, Guid conversationId)
        {
            var userId = Guid.Parse(user.Id);
            var conversation = await _context.StandaloneConversations
                .Include(c => c.ChatSystemMessage)
                .Include(c => c.UserMessages)
                .Include(c => c.ModelMessages)
                .Include(c => c.ToolMessages)
                .FirstOrDefaultAsync(c => c.ChatConversationID == conversationId && c.UserID == userId);

            if (conversation == null)
            {
                return Result<LHChatConversationDTO, Problem>.Failure(new Problem.NotFound("Conversation not found."));
            }

            return Result<LHChatConversationDTO, Problem>.Success(conversation.ToDTO());
        }

        public async Task<Result<LHChatConversationDTO, Problem>> GetMostRecentChatConversationAsync(IdentityUser user)
        {
            var userId = Guid.Parse(user.Id);
            var conversation = await _context.StandaloneConversations
                .Where(c => c.UserID == userId)
                .OrderByDescending(c => c.LastUpdate)
                .Include(c => c.UserMessages)
                .Include(c => c.ModelMessages)
                .FirstOrDefaultAsync();
            if (conversation == null)
            {
                return Result<LHChatConversationDTO, Problem>.Failure(new Problem.NotFound("No conversations found."));
            }
            return Result<LHChatConversationDTO, Problem>.Success(conversation.ToDTO());


        }

        public async Task<Result<LHChatConversationDTO, Problem>> UpdateChatConversationAsync(IdentityUser user, UpdateChatConversationRequest request)
        {
            var userId = Guid.Parse(user.Id);
            var conversation = await _context.StandaloneConversations
                .FirstOrDefaultAsync(c => c.ChatConversationID == request.ChatConversationID && c.UserID == userId);

            if (conversation == null)
            {
                return Result<LHChatConversationDTO, Problem>.Failure(new Problem.NotFound("Conversation not found."));
            }

            conversation.Name = request.Name;
            conversation.LastUpdate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Result<LHChatConversationDTO, Problem>.Success(conversation.ToDTO());
        }

        
    }
}