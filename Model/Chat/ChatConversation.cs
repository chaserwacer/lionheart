using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Model.Chat.Completion;
using OpenAI.Chat;
namespace lionheart.Model.Chat
{
    ////////NOTES
    /// All of these models have been prefixed with "LH" to avoid confusion with OpenAI SDK models (ChatMessage, ChatConversation, etc).

    /// <summary>
    /// Represents a chat conversation that contain a collection of <see cref="LHChatMessage"/>.
    /// This entity corresponds to a chat session between a user and some AI model. 
    /// </summary>
    public class LHChatConversation
    {
        [Key]
        public required Guid ChatConversationID { get; init; }
        public required Guid UserID { get; init; }
        public required DateTime CreatedAt { get; init; } = DateTime.UtcNow;
        public required DateTime LastUpdate { get; set; }
        public required string Name { get; set; } = string.Empty;
        /// <summary>
        /// System Instructions for the model, guiding behavior. 
        /// </summary>
        /// <remarks>
        /// In effort to reduce total tokens, each Chat Conversation will only have one of these. 
        /// If we want to modify model behavior, we can modify this property, as opposed to adding an additional. 
        /// This SystemChatMessage should: (likely) be defined internally, always fed to chat completions, never summarized/compressed during prompting.
        /// </remarks>
        public required LHSystemChatMessage ChatSystemMessage { get; set; }
        /// <summary>
        /// Messages sent from the AI model. 
        /// </summary>
        public required List<LHModelChatMessage> ModelMessages { get; set; }
        /// <summary>
        /// Messages sent by the user. 
        /// </summary>
        public required List<LHUserChatMessage> UserMessages { get; set; }
        public required List<LHToolChatMessage> ToolMessages { get; set; }


        public List<LHChatMessage> GetAllNonSystemMessagesInChronologicalOrder()
        {
            var allMessages = new List<LHChatMessage>();
            allMessages.AddRange(UserMessages);
            allMessages.AddRange(ModelMessages);
            allMessages.AddRange(ToolMessages);
            allMessages.Sort((x, y) => x.CreationTime.CompareTo(y.CreationTime));
            return allMessages;
        }
        public List<LHChatMessage> GetUserModelMessagesInChronologicalOrder()
        {
            var allMessages = new List<LHChatMessage>();
            allMessages.AddRange(UserMessages);
            allMessages.AddRange(ModelMessages);
            allMessages.Sort((x, y) => x.CreationTime.CompareTo(y.CreationTime));
            return allMessages;
        }

        public LHChatConversationDTO ToDTO()
        {

            var messages = new List<LHChatMessageDTO>();
            foreach (var msg in GetUserModelMessagesInChronologicalOrder())
            {
                messages.Add(new LHChatMessageDTO(msg));
            }

            return new LHChatConversationDTO
            {
                ChatConversationID = ChatConversationID,
                CreatedAt = CreatedAt,
                LastUpdate = LastUpdate,
                Name = Name,
                Messages = messages
            };


        }


    }

    /// <summary>
    /// Represent a base lionheart chat message.
    /// </summary>
    public abstract class LHChatMessage()
    {
        [Key]
        public required Guid ChatMessageItemID { get; init; }
        [ForeignKey(nameof(ChatConversation))]
        public required Guid ChatConversationID { get; init; }
        public LHChatConversation ChatConversation { get; set; } = null!;
        public required DateTime CreationTime { get; init; }

        /// <summary>
        /// Integer count of tokens number for this object.
        /// </summary>
        /// <remarks>
        /// Use the OpenAI ChatTokenUsage object to retreive this number if possible.
        /// </remarks>
        public required int TokenCount { get; set; }
        public required string? Content { get; set; }
        /// <summary>
        /// Convert this LH chat message to its corresponding OpenAI ChatMessage.
        /// </summary>
        public abstract ChatMessage ToChatMessage();
    }


    /// <summary>
    /// Represents a chat message created by a <see cref="User.LionheartUser"/> 
    /// This contains a text message the user is sending to the model. 
    /// </summary>
    public class LHUserChatMessage : LHChatMessage
    {
        public override ChatMessage ToChatMessage()
        {
            return new UserChatMessage(Content);
        }
    }

    /// <summary>
    /// Represents a chat message that is created by an AI model. 
    /// This contains potential messages 
    /// </summary>
    public class LHModelChatMessage : LHChatMessage
    {
        public IEnumerable<ChatToolCall> ToolCalls { get; init; } = new List<ChatToolCall>();
        public override ChatMessage ToChatMessage()
        {
            if (ToolCalls is not null && ToolCalls.Any())
            {
                return new AssistantChatMessage(ToolCalls);
            }
            return new AssistantChatMessage(Content);
            
        }
    }

    /// <summary>
    /// Represents the system chat message that provides instructions to the AI model.
    /// </summary>
    public class LHSystemChatMessage : LHChatMessage
    {
        public override ChatMessage ToChatMessage()
        {
            return new SystemChatMessage(Content);
        }
    }

    public class LHToolChatMessage : LHChatMessage
    {
        public required string ToolCallID { get; set; }

        public override ChatMessage ToChatMessage()
        {
            return new ToolChatMessage(ToolCallID, Content);
        }
    }


}