using System.ComponentModel.DataAnnotations;
using lionheart.Model.Chat;
using OpenAI.Chat;

namespace Model.Chat.Completion
{
    /// <summary>
    /// Object representing a chat completion request.
    /// This contains the messages, options, and other relevant data for generating a chat completion.
    /// 
    /// </summary>
    public record ChatCompletionRequest
    {
        [Required]
        public required LHChatConversation Conversation { get; set; }
        [Required]
        public required ChatCompletionOptions Options { get; set; }

        
    }

    public record ChatCompletionResponse
    {
        public required ChatCompletion Completion { get; set; }
        public required List<LHChatMessage> CompletionGeneratedMessages { get; set; }
        public required Guid ModelFinalChatMessageID { get; set; }
    }
    


  
}