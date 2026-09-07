using lionheart.Model.Analysis;
using lionheart.Model.Training;

namespace lionheart.Model.Contexts;

// Two creates rather than one polymorphic body (docs/api-surface.md): flat request shapes
// generate a far better TypeScript client, and each kind states exactly what it has. Neither
// carries a ContextId, a UserId or a Decoration — all three are the server's.

/// <summary>Physical exertion outside a training session: pressure-washing, moving house, a hike.</summary>
public sealed record CreateExertionContextRequest(
    PeriodCommand Period,
    string Notes,
    PerceivedEffortCommand? Effort);

/// <summary>A body or life state with no exertion payload: bad sleep, exam week, an illness.</summary>
public sealed record CreateStateContextRequest(PeriodCommand Period, string Notes);

/// <summary>Notes are commentary, editable without changing what kind of context this is.</summary>
public sealed record ContextNotesRequest(string Notes);
