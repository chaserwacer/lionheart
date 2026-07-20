# Lionheart Data Remodel

Reference design for the training-data remodel. Decisions here are settled unless marked
open. Build against this document.

## Motivation

The existing hierarchy (Session → Movement → MovementData → Lift/DT sets) demands full
structure on every log. Real logging is often terse ("sled pull+push worked up to 235,
2 or 3 sets"). **Fidelity is a first-class axis**: looseness is a genuinely smaller typed
shape, never nullable columns on a god-schema. Every entry — however loose — must still
feed analytics and RAG.

## Principles

- Typed domain, JSON storage. Sealed record hierarchies in C#; polymorphic lists persist
  as jsonb. Impossible states are unrepresentable; storage stays schema-flexible.
- Structuring is additive enrichment, never replacement. The raw utterance survives any
  upgrade to a richer type.
- Two annotation channels on everything analyzable: **facets** (closed vocabulary →
  deterministic SQL analytics) and **embedding + valence** (semantic → RAG, emergent
  analytics).
- One interpretation engine (Parse → Resolve → Commit), versioned and re-runnable, serves
  ingest, freeform→structured upgrades, and full-history backfill.

## Domain model

### Period (shared temporal shape)

```csharp
public abstract record Period;
public sealed record Instant(DateTime At) : Period;
public sealed record Interval(DateTime Start, DateTime End) : Period;
```

### Decoration (the annotation payload)

```csharp
public sealed record Decoration(
    IReadOnlyList<Facet> Facets,      // closed-vocab (axis, canonical value)
    IReadOnlyList<string> FreeTags,   // open labels for semantic recall
    Valence Valence,                  // Positive | Negative | Neutral | Mixed
    string SummaryText,               // what gets embedded; vector lives on AnalyzableEntry
    int DecoratorVersion);            // which extractor produced this

public sealed record Facet(FacetAxis Axis, string Value);
public enum FacetAxis { Modality, MovementPattern, MuscleRegion, Intent, LifeDomain, BodyState }
```

**Axes are fixed; values grow.** Canonical values live in a lookup table; the LLM proposes
values and a semantic resolver snaps near-matches to existing canonicals (accumulating
aliases) or admits new canonicals below a similarity threshold — the same
`UnresolvedReference`/`EntityMatch` flow already used for movements and equipment.

```csharp
public sealed record FacetValue(
    FacetAxis Axis, string Canonical,
    IReadOnlyList<string> Aliases, float[] Embedding);
```

Decoration fill-state is explicit, never null:

```csharp
public abstract record DecorationState;
public sealed record NotDecorated : DecorationState;
public sealed record Decorated(Decoration Decoration) : DecorationState;
```

**Timing: sessions are decorated on completion, not intra-session.** Contexts are
decorated at creation.

### Utterance (how an entry was logged)

```csharp
public abstract record Utterance;
public sealed record Texted(string RawText, double StructureConfidence) : Utterance;
public sealed record Formed : Utterance;
```

Freeform logging is a **first-class choice**, not a fallback. When the model can lift
structure from a texted entry it does (Texted source survives on the structured type);
when it can't, the entry stays `FreeformSet`. Upgrading later is the state transition
`FreeformSet → LiftSet` etc., re-running the parse — the words are never lost.
`StructureConfidence` carries ambiguity ("2 or 3 sets") without blocking the log.

### SetEntry (sealed, per-modality shapes)

Flat ordered list on the session. Movement grouping is derivable from consecutive
`MovementDataId`s — the old `Movement` wrapper entity is deleted. `MovementData`
(base + equipment + modifier identity) is kept; PRs hang off it.

```csharp
public abstract record SetEntry
{
    public required Utterance Source { get; init; }
    public required IReadOnlyList<Facet> Facets { get; init; }  // per-set facets (cheap);
}                                                               // embeddings stay session-level

public sealed record LiftSet(
    Guid MovementDataId, int Reps, double Weight, WeightUnit Unit, Rpe Rpe) : SetEntry;
public sealed record DistanceTimeSet(
    Guid MovementDataId, double Distance, DistanceUnit Unit, TimeSpan Duration, Rpe Rpe) : SetEntry;
public sealed record TrailRunSet(
    double Distance, DistanceUnit Unit, TimeSpan Duration, double ElevationGainMeters, Rpe Rpe) : SetEntry;
public sealed record FreeformSet : SetEntry;   // structure lives in Source.RawText + Facets
```

Pace is derived (distance ÷ duration), not stored. Optional-but-common values get
encapsulated fill-state instead of nulls:

```csharp
public readonly record struct Rpe   // Rpe.Unrated or Rpe.Of(8.5)
{
    public bool IsRated { get; }
    public double Value { get; }    // valid only when IsRated
}
```

### TrainingSession (lifecycle as sealed subtypes)

Status enum (incl. the provenance-smuggling `AIModified`) is replaced by subtypes whose
fields match their state — prescriptions live only on planned, actuals only on completed:

```csharp
public abstract record TrainingSession
{
    public required Guid TrainingSessionId { get; init; }
    public required Guid UserId { get; init; }
    public required string Notes { get; init; }
    public required Provenance Provenance { get; init; }   // User | AiAssisted — orthogonal to lifecycle
    public required ProgramMembership Membership { get; init; } // Standalone | InProgram — fill-state, not nullable FK
    public abstract DateOnly Date { get; }                 // derived from each state's own time-truth
}

public sealed record PlannedTrainingSession(DateOnly PlannedFor, IReadOnlyList<SetPrescription> Prescriptions) : TrainingSession;
public sealed record ActiveTrainingSession(DateTime StartedAt, IReadOnlyList<SetEntry> Sets) : TrainingSession;
public sealed record CompletedTrainingSession(
    IReadOnlyList<SetEntry> Sets,
    Period Period,                 // when it actually happened (Instant, or Interval w/ duration)
    EffortState Effort,            // EffortNotReported | EffortReported(PerceivedEffortRatings)
    DecorationState Decoration) : TrainingSession, IAnalyzable;
public sealed record SkippedTrainingSession(DateOnly PlannedFor) : TrainingSession;
```

`SetPrescription` mirrors the set hierarchy with target values (the old `Recommended*`
fields), so planned/actual never half-null each other on one row.

### TrainingProgram

`IsCompleted: bool` → sealed lifecycle subtypes:

```csharp
public abstract record TrainingProgram { /* Id, UserId, Title, StartDate, EndDate, Tags */ }
public sealed record PlannedTrainingProgram(...) : TrainingProgram;
public sealed record ActiveTrainingProgram(...) : TrainingProgram;
public sealed record CompletedTrainingProgram(...) : TrainingProgram;  // can carry a summary
```

### Context (replaces Activity)

Anything non-session with performance relevance. Kind (sleep, stress, pressure-washing)
is a **facet**, not a subtype — subtypes exist only where fields genuinely differ:

```csharp
public abstract record Context
{
    public required Guid ContextId { get; init; }
    public required Guid UserId { get; init; }
    public required Period Period { get; init; }        // Instant or Interval
    public required string Notes { get; init; }
    public required DecorationState Decoration { get; init; }
}

public sealed record ExertionContext(PerceivedEffortRatings Effort) : Context; // physical exertion outside training
public sealed record StateContext : Context;                                   // body/life state; valence in Decoration
```

Duration lives in `Period.Interval` — no separate minutes field.

## Analysis surface

### IAnalyzable + AnalyzableEntry

Domain contract for anything the analytics/RAG layer can consume:

```csharp
public interface IAnalyzable
{
    DecorationState Decoration { get; }
    Period Period { get; }
}
```

`CompletedTrainingSession` and `Context` implement it. Because EF can't query across
tables through an interface, decoration **also** writes a row into a denormalized read
model — the single backing store for retrieval:

```csharp
public sealed record AnalyzableEntry(
    Guid EntryId, EntryKind Kind, Guid UserId,   // Kind = Session | Context
    Period Period,
    IReadOnlyList<Facet> Facets,                  // GIN-indexed jsonb
    Valence Valence,
    float[] Embedding,                            // pgvector
    string SummaryText,
    int DecoratorVersion);
```

### The retrieval primitive

```
retrieve(facets?, window?, semantic?, valence?) → ranked AnalyzableEntry
```

- App analytics: facets + window (balance ledger, avoidance map, ROI, rivalry).
- RAG / workout-texting chat: semantic + window.
- Hidden-cost finder: two calls joined on time (sessions vs next-day contexts/wellness).
- **MCP tool surface = this primitive.** Fixed `FacetAxis` set becomes the tool's enum
  params — the closed axes are what keep the tool description finite and calls valid.

## Interpretation engine

Generalization of the existing ingestion pipeline (`Model/Ingestion/IngestionModels.cs`):

1. **Parse** (LLM): raw text → sets *and* a proposed `Decoration`; runs over Contexts too.
2. **Resolve**: snap entities (movements/equipment) *and* facet values to canonicals via
   embedding similarity; unresolved → user confirmation (existing flow).
3. **Commit**: write domain entity + upsert its `AnalyzableEntry` row.

Every decoration is stamped with `DecoratorVersion`. One engine, three clients:
initial ingest, freeform→structured upgrade, backfill (`WHERE DecoratorVersion < current`).

**Embedding-model changes force a full recompute** — vectors from different models are
incomparable. Extractor-prompt improvements may re-decorate incrementally; embedding
swaps may not be partial.

## Storage

**Provider: SQLite** (decided 2026-07-19 — the app already runs on it; at single-user
volume nothing here degrades). The retrieve primitive's interface is provider-shaped, so
a later Postgres/pgvector swap stays contained in the storage layer.

- Relational spine: sessions, programs, contexts, MovementData, FacetValue as tables.
- Polymorphic shapes (`SetEntry` lists, `SetPrescription` lists, `Period`,
  `DecorationState`, `EffortState`, `Utterance`) as JSON TEXT columns via
  System.Text.Json polymorphic serialization (`[JsonDerivedType]`, discriminator
  `$type`) behind EF value converters (EF's native owned-JSON doesn't handle
  hierarchies). `Rpe` maps rated→number, unrated→null (`RpeJsonConverter`).
- Embeddings: float[] as BLOB; cosine similarity computed in-app — fine at personal
  scale, replaced by pgvector only if volume ever demands it.
- Queryable time: derived `Date`/`StartTime` values are duplicated into real indexed
  columns at save time (recomputed on every save by the persistence layer, so they
  cannot drift from the domain truth). Facet filtering loads-and-filters in app.

## Migration & backfill

**Old data is backfilled into the new model** (decided). Old tables stay until backfill
verifies clean.

- `Activity` rows → `ExertionContext` (Formed utterance semantics; decorate via engine).
- Old sessions/movements/sets → new sessions with structured sets (`Source = Formed`),
  decorated by the engine at current version.
- Backfill is the interpretation engine's first bulk client — no bespoke migration logic.

## Out of scope (unchanged for now)

Chat, Oura, Profile, Wellness, Injury.

## Decision log

| Decision | Resolution |
|---|---|
| Typed vs JSON | Typed sealed domain; jsonb is storage only |
| Freeform logging | First-class choice; model upgrades to rich types when it can; utterance always survives |
| Facet vocabulary | Axes fixed, values growable via semantic resolve + alias accumulation |
| Valence | Lives in Decoration (shared), not on Context |
| Decoration granularity | Facets per-set; embeddings/valence per-session |
| Decoration timing | Sessions: on completion only. Contexts: on creation |
| Session status | Sealed lifecycle subtypes; provenance is a separate axis (kills `AIModified`) |
| Program status | `Planned/Active/CompletedTrainingProgram` subtypes (kills `IsCompleted`) |
| Activity | Deleted; absorbed by `ExertionContext` |
| Movement wrapper | Deleted; flat ordered set list, grouping derivable. `MovementData` kept |
| Old data | Backfilled through the interpretation engine |
| Session date | Derived (`abstract Date`) from each lifecycle state's own time-truth (PlannedFor / StartedAt / Period) — never stored alongside it |
| Storage provider | SQLite (already in use; no quality loss at this scale). Retrieval interface stays provider-shaped for a contained Postgres swap later |
| Embedding storage | No EmbeddingRef indirection — vectors live directly on `FacetValue` and the `AnalyzableEntry` projection; `Decoration` carries `SummaryText` |

## Status & roadmap — source of truth

Update this section as steps land. Branch: `claude/lionheart-data-remodel-684517`.

### ✅ Step 1 — Domain core (commit `36dfb47`)

Sealed hierarchies, exactly as specced above:

- `Model/Analysis/` — `Period`, `Decoration` + `Facet`/`FacetAxis`/`Valence`/
  `DecorationState`, `FacetValue`, `IAnalyzable`, `AnalyzableEntry`.
- `Model/Training/Sets/` — `Utterance`, `Rpe`, `SetEntry` hierarchy.
- `Model/Training/Sessions/` — session lifecycle subtypes, `Provenance`,
  `ProgramMembership`, `SetPrescription` hierarchy.
- `Model/Training/Programs/TrainingProgram.cs`, `Model/Training/EffortState.cs`,
  `Model/Contexts/Context.cs`.

Passed a code-review-fixer pass. Gotcha (commented in-code): the legacy
`lionheart.Model.Training.SetEntry` *namespace* shadows the new `SetEntry` *type*;
`Sessions/TrainingSession.cs` carries a namespace-scoped using alias until the legacy
namespace dies.

### ✅ Step 2 — Persistence (commit `f63465a`)

- `Data/ValueConversions.cs` — polymorphic records ↔ JSON TEXT (`$type` discriminators
  on the abstract bases), `float[]` ↔ BLOB.
- `Data/RemodelConfiguration.cs` — TPH mapping for the five new tables; deliberately
  separate from the legacy inline config so old mapping deletes wholesale after backfill.
- `ModelContext` — new DbSets (`Sessions`, `Programs`, `Contexts`, `AnalyzableEntries`,
  `FacetValues`); `StampDerivedTimeColumns` recomputes queryable `StartTime` columns
  from JSON `Period` on every save (drift-proof by construction).
- Migration `20260719204913_DataRemodelCore` — five additive tables, legacy untouched,
  applied to the dev DB.
- Gotcha (commented in-code): `ProgramMembership` persists as Guid with
  `Guid.Empty` = Standalone, so `Sessions.TrainingProgramId` has no relational FK.
- Gotcha (fixed on `claude/dotnet-testing-suite-b7e8c7`): the `.gitignore` rule `Data/*`
  silently kept `Data/ValueConversions.cs` and `Data/RemodelConfiguration.cs` out of
  commit `f63465a` — they existed only in the remodel worktree. The rule now ignores
  `Data/*.db*` patterns only, and both files are committed.

### ✅ Step 2.5 — Test suite (branch `claude/dotnet-testing-suite-b7e8c7`)

`lionheart.Tests` (xUnit, `dotnet test`): domain invariants (Rpe, Interval, Texted
confidence, derived `Date`), JSON polymorphic round-trips for every sealed hierarchy with
pinned `$type` discriminators (renaming one orphans stored rows — the tests make that
loud), and EF round-trips against real in-memory SQLite: every session/program lifecycle
state, both context kinds, `AnalyzableEntry` BLOB embeddings, `FacetValue` composite key,
the `Guid.Empty` membership sentinel at the raw-column level, and `StartTime` stamping on
insert + update + window query. This delivers step 3's smoke-test prerequisite up front.

### ⬜ Step 3 — Interpretation engine

Generalize `Model/Ingestion/IngestionModels.cs` into the versioned
Parse → Resolve → Commit engine:

- Parse emits sets **and** a proposed `Decoration`; runs over Context text too.
- Resolve snaps facet values to `FacetValue` canonicals via embedding similarity
  (same pattern as the existing `UnresolvedReference`/`EntityMatch` flow).
- Commit writes the domain entity and upserts its `AnalyzableEntry` row.
- Stamp `DecoratorVersion` everywhere; engine must be re-runnable (freeform upgrades,
  backfill, re-decoration).
- ~~First deliverable: a runtime JSON round-trip smoke test~~ — **done in step 2.5**:
  `lionheart.Tests` exercises save + rehydrate for every session lifecycle state, both
  context kinds, and `AnalyzableEntry` against real SQLite.

### ⬜ Step 4 — Backfill

Legacy rows through the engine (decided; legacy tables stay until verified):

- `Activity` → `ExertionContext` (`Formed` utterance semantics).
- Legacy sessions/Movements/Lift+DT sets → new sessions with structured sets
  (`Source = Formed`), decorated at current version.
- After verification: drop legacy entities, tables, services, endpoints — including the
  legacy `SetEntry` namespace (removes the alias gotcha) and the `Activity` model.

### ⬜ Step 5 — Balance ledger (first analytic)

Pure-facet running balances (push/pull, quad/hinge, lift/run) over `AnalyzableEntry`.
Chosen first because it validates the facet vocabulary + resolution chain end to end.

### ⬜ Step 6 — Retrieval primitive + MCP surface

`retrieve(facets?, window?, semantic?, valence?)` over `AnalyzableEntries` (facet filter
+ time window in SQL; cosine over BLOB vectors in-app). MCP tool = this primitive with
`FacetAxis` as enum params. Then the remaining analytics (sentiment arc, avoidance map,
hidden-cost, eras, rivalry, ROI) as consumers.

### Open questions

1. ~~Test infrastructure~~ — resolved: dedicated `lionheart.Tests` xUnit project
   (step 2.5); persistence tests run the full `ModelContext` schema on in-memory SQLite.
2. Three hand-rolled fill-states (`DecorationState`, `EffortState`,
   `ProgramMembership`) — generic `Option<T>` deferred unless a fourth appears.
3. DTO/request shapes for the new types and the migration path for existing
   endpoints/services (likely lands with steps 3–4).
