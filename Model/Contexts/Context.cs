using lionheart.Model.Analysis;
using lionheart.Model.Training;

namespace lionheart.Model.Contexts;

/// <summary>
/// Anything non-session with performance relevance — pressure-washing, exam stress, bad
/// sleep. Kind is a facet in the decoration, not a subtype; subtypes exist only where
/// fields genuinely differ.
/// </summary>
public abstract record Context : IAnalyzable
{
    public required Guid ContextId { get; init; }
    public required Guid UserId { get; init; }

    /// <summary>An instant ("terrible sleep last night") or an interval ("exam week").</summary>
    public required Period Period { get; init; }
    public required string Notes { get; init; }
    public required DecorationState Decoration { get; init; }
}

/// <summary>Physical exertion outside a training session — carries effort, which a pure state has no business having.</summary>
public sealed record ExertionContext(EffortState Effort) : Context;

/// <summary>A body/life state with no exertion payload; valence lives in the decoration.</summary>
public sealed record StateContext : Context;
