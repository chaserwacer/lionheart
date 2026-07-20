using lionheart.Model.Analysis;
using lionheart.Model.Contexts;
using lionheart.Model.Training;
using lionheart.Model.Training.Sessions;
using lionheart.Model.Training.Sets;

namespace lionheart.Model.Interpretation;

public sealed record InterpretationRequest(string RawText, DateTime LoggedAt);

/// <summary>
/// A facet value as the parser proposed it — arbitrary spelling, not yet a member of the
/// <see cref="FacetValue"/> vocabulary. Resolve turns this into a <see cref="Facet"/>.
/// </summary>
public sealed record ProposedFacet(FacetAxis Axis, string Value);

/// <summary>A <see cref="Decoration"/> before canonicalization and version stamping.</summary>
public sealed record ProposedDecoration(
    IReadOnlyList<ProposedFacet> Facets,
    IReadOnlyList<string> FreeTags,
    Valence Valence,
    string SummaryText);

/// <summary>
/// A movement named rather than identified. The parser only ever knows names; Resolve maps
/// them onto a <c>MovementData</c> id, which is what the domain set types require.
/// </summary>
public sealed record MovementReference(string BaseName, string EquipmentName, string ModifierName);

/// <summary>
/// A set as parsed: the domain <see cref="SetEntry"/> shapes, but keyed by movement name
/// instead of id. Shapes correspond one-to-one so Resolve is a straight mapping.
/// </summary>
public abstract record DraftSet
{
    public required Utterance Source { get; init; }
    public required IReadOnlyList<ProposedFacet> Facets { get; init; }
}

public sealed record DraftLiftSet(
    MovementReference Movement,
    int Reps,
    double Weight,
    WeightUnit Unit,
    Rpe Rpe) : DraftSet;

public sealed record DraftDistanceTimeSet(
    MovementReference Movement,
    double Distance,
    DistanceUnit Unit,
    TimeSpan Duration,
    Rpe Rpe) : DraftSet;

public sealed record DraftTrailRunSet(
    double Distance,
    DistanceUnit Unit,
    TimeSpan Duration,
    double ElevationGainMeters,
    Rpe Rpe) : DraftSet;

/// <summary>No structure could be lifted — the utterance itself is the record.</summary>
public sealed record DraftFreeformSet : DraftSet;

/// <summary>
/// One analyzable thing the parser found in the text. The subtypes mirror the two
/// analyzable aggregates — a training session, or a context of either kind.
/// </summary>
public abstract record DraftEntry
{
    public required Period Period { get; init; }
    public required string Notes { get; init; }
    public required ProposedDecoration Decoration { get; init; }
}

public sealed record DraftSession(IReadOnlyList<DraftSet> Sets, EffortState Effort) : DraftEntry;

public sealed record DraftExertionContext(EffortState Effort) : DraftEntry;

public sealed record DraftStateContext : DraftEntry;

public sealed record ParsedInterpretation(
    IReadOnlyList<DraftEntry> Entries,
    IReadOnlyList<string> Warnings);

public enum ReferenceKind { MovementBase, Equipment }

public sealed record EntityMatch(Guid EntityId, string Name, double Confidence);

public sealed record UnresolvedReference(
    string RawName,
    ReferenceKind Kind,
    IReadOnlyList<EntityMatch> Candidates);

/// <summary>
/// Resolve's output once every reference landed: real domain records, ready to persist.
/// Sessions and contexts are separated because Commit writes them to different aggregates.
/// </summary>
public sealed record ResolvedInterpretation(
    IReadOnlyList<CompletedTrainingSession> Sessions,
    IReadOnlyList<Context> Contexts,
    IReadOnlyList<string> Warnings);

/// <summary>Where the resolve stage landed: every reference mapped, or some names are ambiguous.</summary>
public abstract record ResolutionOutcome;

public sealed record Resolved(ResolvedInterpretation Interpretation) : ResolutionOutcome;

public sealed record Ambiguous(IReadOnlyList<UnresolvedReference> References) : ResolutionOutcome;

/// <summary>
/// Where a run of the engine ended: everything resolved and was written, or the user has to
/// disambiguate a name the parser used before anything can be committed.
/// </summary>
public abstract record InterpretationOutcome;

public sealed record Committed(
    IReadOnlyList<Guid> SessionIds,
    IReadOnlyList<Guid> ContextIds,
    IReadOnlyList<string> Warnings) : InterpretationOutcome;

/// <summary>
/// Carries the draft back with the ambiguities so a later confirmation round can re-resolve
/// it against the user's choices without re-parsing (and re-paying for) the text.
/// </summary>
public sealed record AwaitingConfirmation(
    ParsedInterpretation Draft,
    IReadOnlyList<UnresolvedReference> References) : InterpretationOutcome;
