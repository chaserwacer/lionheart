using System.Text.Json;
using lionheart.Functional;
using lionheart.Model.Chat.Memory;
using lionheart.Services.Llm;

namespace lionheart.Services.Chat.Thread;

/// <summary>The changes a consolidating model proposed, and the ones it got wrong.</summary>
/// <param name="Unusable">Why each rejected change was rejected — a handle that names nothing, an empty text.</param>
public sealed record ConsolidationPlan(IReadOnlyList<MemoryChange> Changes, IReadOnlyList<string> Unusable);

/// <summary>
/// Reads the consolidating model's answer into memory changes.
/// </summary>
/// <remarks>
/// An answer that is not the object asked for fails the consolidation, so the session is tried
/// again. One bad change inside a good answer does not: it is set aside and the rest are kept,
/// since the model's mistake about one memory says nothing about the others.
/// </remarks>
public static class ConsolidationParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Result<ConsolidationPlan, Problem> Read(ModelTurn turn, ConsolidationPrompt prompt) =>
        Answer(turn)
            .Bind(Deserialize)
            .Map(plan => (plan.Changes ?? []).Partition(change => Change(change, prompt)))
            .Map(partitioned => new ConsolidationPlan(partitioned.Delivered, partitioned.Refused));

    private static Result<string, Problem> Answer(ModelTurn turn) =>
        turn switch
        {
            ModelTurn.Answered { Text: var text } when !string.IsNullOrWhiteSpace(text) =>
                Result<string, Problem>.Success(text),
            ModelTurn.Answered => Result<string, Problem>.Failure(new Problem.Upstream(
                "The consolidating model returned an empty answer. If it reasons, the answer may have gone to its " +
                "reasoning channel — see the \"Traps\" section of docs/data-remodel.md.")),
            ModelTurn.RequestedTools => Result<string, Problem>.Failure(new Problem.Upstream(
                "The consolidating model asked for a tool; consolidation offers none.")),
            _ => throw new System.Diagnostics.UnreachableException(),
        };

    private static Result<ProposedPlan, Problem> Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ProposedPlan>(json, JsonOptions) is { } plan
                ? Result<ProposedPlan, Problem>.Success(plan)
                : Result<ProposedPlan, Problem>.Failure(new Problem.Upstream("The consolidating model returned null."));
        }
        catch (JsonException exception)
        {
            return Result<ProposedPlan, Problem>.Failure(new Problem.Upstream(
                $"The consolidating model returned malformed JSON: {exception.Message}"));
        }
    }

    private static Result<MemoryChange, string> Change(ProposedChange change, ConsolidationPrompt prompt)
    {
        var sources = Sources(change, prompt);
        var text = change.Text?.Trim() ?? string.Empty;

        return (change.Action?.Trim().ToLowerInvariant(), Kind(change.Kind)) switch
        {
            (_, null) => Refuse($"'{change.Kind}' is not a kind of memory."),
            ("remember", _) when text.Length == 0 => Refuse("A memory to remember had no text."),
            ("remember", { } kind) => Result<MemoryChange, string>.Success(new MemoryChange.Remember(kind, text, sources)),
            ("revise", { } kind) => Kept(change, prompt).Bind(memory => text.Length == 0
                ? Refuse($"The revision of {change.MemoryId} had no text.")
                : Result<MemoryChange, string>.Success(new MemoryChange.Revise(memory.MemoryId, kind, text, sources))),
            ("retire", _) => Kept(change, prompt).Map(memory => (MemoryChange)new MemoryChange.Retire(
                memory.MemoryId, text.Length == 0 ? "No longer holds." : text, sources)),
            var (action, _) => Refuse($"'{action}' is not a consolidation action."),
        };
    }

    private static Result<ChatMemory, string> Kept(ProposedChange change, ConsolidationPrompt prompt) =>
        prompt.Memories.TryGetValue(change.MemoryId?.Trim() ?? string.Empty, out var memory)
            ? Result<ChatMemory, string>.Success(memory)
            : Result<ChatMemory, string>.Failure($"'{change.MemoryId}' names no kept memory.");

    /// <summary>The messages the change cites, by id. A handle that names no message is dropped.</summary>
    private static IReadOnlyList<Guid> Sources(ProposedChange change, ConsolidationPrompt prompt) =>
        [.. (change.SourceMessageIds ?? [])
            .Select(handle => prompt.Messages.TryGetValue(handle.Trim(), out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()];

    private static MemoryKind? Kind(string? wire) =>
        Enum.TryParse<MemoryKind>(wire?.Trim(), ignoreCase: true, out var kind) && Enum.IsDefined(kind) ? kind : null;

    private static Result<MemoryChange, string> Refuse(string why) => Result<MemoryChange, string>.Failure(why);

    private sealed record ProposedPlan(List<ProposedChange>? Changes);

    private sealed record ProposedChange(
        string? Action, string? MemoryId, string? Kind, string? Text, List<string>? SourceMessageIds);
}
