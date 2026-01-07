using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OpenAI.Chat;
namespace lionheart.Model.Chat
{



    /// Approach Discussion ---
    /// From my understanding, each time you prompt the model, it returns a chat completion. [includes info about finish reason, role, content, tool calls]
    /// I will then convert that chat completion into a given chat message, which will then be stored in the database. 
    ///     The implementation of chat message scould either be one of OpenAIs, or my own custom version. 
    ///     Their implementations are complex objects, im not certain 

    /// <summary>
    /// Represents a chat conversation that contain a collection of <see cref="ChatMessageItem"/>.
    /// This entity corresponds to a chat session between a user and some AI model. 
    /// </summary>
    public class ChatConversation
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
        public required SystemChatMessage SystemChatMessage { get; set; }
        /// <summary>
        /// Messages sent from the AI model. 
        /// </summary>
        public required List<ModelChatMessage> ModelMessages { get; set; }
        /// <summary>
        /// Messages sent by the user. 
        /// </summary>
        public required List<LionheartUserChatMessage> UserMessages { get; set; }
        /// <summary>
        /// Name of the AI model this chat will use. 
        /// </summary>
        public required string ModelName {get; init;}

    }
    /// <summary>
    /// Represent a base lionheart chat message.
    /// </summary>
    public abstract class ChatMessage
    {
        [Key]
        public required Guid ChatMessageItemID { get; init; }
        [ForeignKey (nameof(ChatConversation))]
        public required Guid ChatConversationID { get; init; }
        public ChatConversation ChatConversation { get; set; } = null!;
        public required DateTime CreationTime { get; init; }

        /// <summary>
        /// Integer count of tokens number for this object.
        /// </summary>
        /// <remarks>
        /// Use the OpenAI ChatTokenUsage object to retreive this number if possible.
        /// </remarks>
        public required int TokenCount { get; set; }
        public required string Content { get; set; }
    }

    /// <summary>
    /// Represents a chat message created by a <see cref="User.LionheartUser"/> 
    /// This contains a text message the user is sending to the model. 
    /// </summary>
    public class LionheartUserChatMessage : ChatMessage
    {
        
    }

    /// <summary>
    /// Represents a chat message that is created by an AI model. 
    /// This contains potential messages 
    /// </summary>
    public class ModelChatMessage : ChatMessage
    {
        /// <summary>
        /// Collection of tool chat messages the model used. 
        /// TODO: Validate this item stores the following items needed:
        ///     The request object 
        ///     The method called 
        ///     The reponse from the method 
        /// </summary>
        public required List<ToolChatMessage> ToolChatMessages {get; set;}
    }


    
}