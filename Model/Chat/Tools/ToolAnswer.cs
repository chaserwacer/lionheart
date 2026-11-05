using lionheart.Model.Chat.Changes;
using lionheart.Services.Llm;

namespace lionheart.Model.Chat.Tools;

/// <summary>What running one tool produced: something for the model to read, or a change proposed to the athlete.</summary>
/// <remarks>
/// Two cases because the exchange does different things with them. A reading is told to the model
/// and kept as a tool result; a proposal is told to the model as well, and is also stored with the
/// session as a card the athlete answers. Nothing else about a tool call reaches past the exchange.
/// </remarks>
public abstract record ToolAnswer
{
    private protected ToolAnswer() { }

    /// <summary>What the model is told.</summary>
    public abstract ToolOutcome Outcome { get; }

    public sealed record Read(ToolOutcome Outcome) : ToolAnswer
    {
        public override ToolOutcome Outcome { get; } = Outcome;
    }

    public sealed record Proposed(ToolOutcome Outcome, ProposalDraft Proposal) : ToolAnswer
    {
        public override ToolOutcome Outcome { get; } = Outcome;
    }
}
