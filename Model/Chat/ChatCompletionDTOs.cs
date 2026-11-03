using System.ComponentModel.DataAnnotations;
using lionheart.Model.Chat;

namespace Model.Chat.Completion
{
    /// <summary>
    /// A request to run one exchange with the chat model: the conversation it answers against, and
    /// how much room the answer gets.
    /// </summary>
    /// <remarks>
    /// Tools and provider settings are not here. The tools on offer are the whole registry, and
    /// which model serves the call is configuration, so neither is a per-request choice.
    /// </remarks>
    public record ChatCompletionRequest
    {
        [Required]
        public required LHChatConversation Conversation { get; set; }

        [Required]
        public required int MaxOutputTokens { get; set; }
    }

    /// <summary>
    /// What one exchange produced: the messages worth persisting, and which of them is the answer
    /// to show the athlete.
    /// </summary>
    public record ChatCompletionResponse
    {
        public required List<LHChatMessage> CompletionGeneratedMessages { get; set; }
        public required Guid ModelFinalChatMessageID { get; set; }
    }
}
