namespace lionheart.Model.Interpretation;

/// <summary>
/// What the athlete decided about one name the resolver could not settle. A null
/// <paramref name="ChosenEntityId"/> means none of the candidates fit and the name is something
/// new, to be created under the name as written.
/// </summary>
public sealed record ReferenceChoice(string RawName, ReferenceKind Kind, Guid? ChosenEntityId);

/// <summary>
/// The resume request. The draft the client was handed comes back untouched together with the
/// choices, which is what lets the server re-resolve without paying for a second parse.
/// </summary>
public sealed record ConfirmationRequest(
    ParsedInterpretation Draft,
    IReadOnlyList<ReferenceChoice> Choices);

/// <summary>
/// A raw name as the lookup key it has to be: the kind it names, plus the name folded to one
/// canonical form. "Sled Pull", " sled pull " and "SLED PULL" are one name to the athlete, so
/// they are one key in a choice list and in a list of prompts alike.
/// </summary>
public readonly record struct ReferenceKey(ReferenceKind Kind, string FoldedName)
{
    public static ReferenceKey For(ReferenceKind kind, string rawName) =>
        new(kind, rawName.Trim().ToLowerInvariant());
}

/// <summary>What the athlete has said about one name.</summary>
public abstract record ReferenceDecision
{
    private protected ReferenceDecision() { }

    /// <summary>Nobody has been asked about this name yet, or the answer did not cover it.</summary>
    public sealed record Undecided : ReferenceDecision;

    /// <summary>The athlete picked one of the candidates they were offered.</summary>
    public sealed record ChoseExisting(Guid EntityId) : ReferenceDecision;

    /// <summary>No candidate fit: this name is an entity the athlete does not have yet.</summary>
    public sealed record ChoseNewEntity : ReferenceDecision;

    public static ReferenceDecision Of(Guid? chosenEntityId) =>
        chosenEntityId is { } entityId ? new ChoseExisting(entityId) : new ChoseNewEntity();
}

/// <summary>
/// Every name the athlete has settled, looked up by the raw name the parser used. The default
/// value is the first pass over a draft, where nothing is settled because nobody has been asked
/// — which is why <see cref="For"/> answers <see cref="ReferenceDecision.Undecided"/> for
/// every name rather than needing an instance to exist first.
/// </summary>
public readonly record struct ConfirmedReferences
{
    private readonly IReadOnlyDictionary<ReferenceKey, ReferenceDecision>? _decisions;

    private ConfirmedReferences(IReadOnlyDictionary<ReferenceKey, ReferenceDecision> decisions) =>
        _decisions = decisions;

    /// <summary>Nothing settled — a freshly parsed draft, before anyone has been asked.</summary>
    public static ConfirmedReferences None => default;

    /// <summary>
    /// A later choice for a name replaces an earlier one, so a client that answers the same
    /// prompt twice is answering, not refusing.
    /// </summary>
    public static ConfirmedReferences From(IReadOnlyList<ReferenceChoice> choices)
    {
        var decisions = new Dictionary<ReferenceKey, ReferenceDecision>();
        foreach (var choice in choices)
        {
            decisions[ReferenceKey.For(choice.Kind, choice.RawName)] =
                ReferenceDecision.Of(choice.ChosenEntityId);
        }

        return new ConfirmedReferences(decisions);
    }

    public ReferenceDecision For(ReferenceKind kind, string rawName) =>
        _decisions is not null && _decisions.TryGetValue(ReferenceKey.For(kind, rawName), out var decision)
            ? decision
            : new ReferenceDecision.Undecided();
}
