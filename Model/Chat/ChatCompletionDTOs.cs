using System.ComponentModel.DataAnnotations;
using lionheart.Model.Chat;
using lionheart.Model.Chat.Changes;
using lionheart.Model.Chat.Tools;

namespace Model.Chat.Completion
{
    /// <summary>
    /// A request to run one exchange with the chat model: the conversation it answers against, and
    /// how much room the answer gets.
    /// </summary>
    /// <remarks>
    /// Provider settings are not here: which model serves the call is configuration, not a
    /// per-request choice. Which tools are on offer is, because only the thread can show a proposal.
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

        /// <summary>Told the answer's text as it is written, and each round of tools as it is run.</summary>
        public IExchangeObserver Observer { get; init; } = ExchangeObserver.Unwatched;

        /// <summary>The tools on offer: reading only, unless the caller can put a proposal in front of the athlete.</summary>
        public ToolAccess Tools { get; init; } = ToolAccess.Read;
    }

    /// <summary>
    /// What one exchange produced: the messages worth persisting, and which of them is the answer
    /// to show the athlete.
    /// </summary>
    public record ChatCompletionResponse
    {
        public required List<LHChatMessage> CompletionGeneratedMessages { get; set; }
        public required Guid ModelFinalChatMessageID { get; set; }

        /// <summary>The changes the model proposed on the way, for the caller to store with the session as cards.</summary>
        public IReadOnlyList<ProposalDraft> Proposals { get; init; } = [];
    }
}
