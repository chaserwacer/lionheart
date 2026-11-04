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
        /// <summary>
        /// The output cap for one exchange. Reasoning models spend output tokens thinking before they
        /// answer, so the cap has to cover both; both providers reason by default, and on Claude
        /// Opus 5.5 thinking cannot be switched off.
        /// </summary>
        public const int ExchangeOutputTokens = 32_000;

        [Required]
        public required LHChatConversation Conversation { get; set; }

        [Required]
        public required int MaxOutputTokens { get; set; }

        /// <summary>
        /// The athlete's day as this exchange begins (see <c>Services/Chat/Today</c>), sent with
        /// every round of the exchange and stored with none of them. Empty sends no briefing.
        /// </summary>
        public string Briefing { get; init; } = string.Empty;
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
