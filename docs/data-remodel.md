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

**No backfill** (decided 2026-07-19, superseding the earlier backfill plan): legacy
training data was abandoned and the legacy model/tables dropped outright. Surviving
legacy entities: `MovementData`/`MovementBase`/`MovementModifier`, `Equipment`,
`MuscleGroup`, `PersonalRecord` (its lift-set source pointer removed; PR *detection*
returns with the interpretation engine).

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
| Old data | Abandoned (user call 2026-07-19) — no backfill; legacy tables dropped. Kept: MovementData/Base/Modifier, Equipment, MuscleGroups, PersonalRecords |
| Session date | Derived (`abstract Date`) from each lifecycle state's own time-truth (PlannedFor / StartedAt / Period) — never stored alongside it |
| Storage provider | SQLite (already in use; no quality loss at this scale). Retrieval interface stays provider-shaped for a contained Postgres swap later |
| Embedding storage | No EmbeddingRef indirection — vectors live directly on `FacetValue` and the `AnalyzableEntry` projection; `Decoration` carries `SummaryText` |
| Period time zone | `DateTimeOffset`: absolute instant indexes the StartTime column, captured offset derives the athlete's calendar `Date`. Offset over zone id — for a past event the offset is the whole answer (2026-07-20) |
| User delete vs. analysis rows | `AnalyzableEntry` cascades with its user — derived data follows its source (2026-07-20) |
| Measure validity | Non-finite rejected at construction; sign deliberately unpoliced (2026-07-20) |
| JSON encoder | Relaxed encoder on storage options — payloads never reach HTML, and readability of the SQLite file wins (2026-07-20) |

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

### ✅ Step 2.6 — Adversarial audit + LLM test infrastructure

Second pass over step 2.5, written to break it rather than confirm it. 76 → 256 tests
(244 running, 8 skipped specifications for the unbuilt interpretation engine, 4 opt-in LocalModel).

**Closed in the suite.** Hand-written JSON literal fixtures pinning the on-disk bytes
(the round-trip tests used the serializer as their own oracle, so a serializer-level bug
was invisible); EF change detection through the value converters, including proof the
vector comparer's cheap `v.Length` hash is safe because its equality is exact;
DateTime fidelity (sub-second ticks and `DateTimeKind` through JSON, precision and
ordering through the stamped column); malformed storage planted by raw SQL and read back
through EF; relational edges (user-delete cascade, FK violations, the unconstrained
program pointer); query translation asserted against generated SQL, not just results;
hostile values (emoji/newlines/quotes, 0- and 1536-length embeddings, non-finite
measures, `DateOnly` extremes, empty lists everywhere); and concurrent stamping across
two contexts on one connection.

**Defects found and fixed** (each has a test that fails without the fix):

- Derived members were being written into the stored JSON — `Period.StartTime`,
  `Interval.Duration`, `DecorationState.IsDecorated`. The design says derived values are
  never stored beside their source; they now carry `[JsonIgnore]`. Note the trap:
  `[JsonIgnore]` does **not** inherit through an override, so every `Period` subtype must
  repeat it.
- `FacetAxis` and `Valence` ordinals outside their enums rehydrated silently, so a
  corrupt row could widen the closed query surface the MCP tool depends on. `Facet`,
  `Decoration`, and `AnalyzableEntry` now guard in their constructors (`Model/Require.cs`),
  which covers deserialization, `with`, and app code alike. The same guards close null
  smuggling into non-nullable members (`Value`, `SummaryText`, `RawText`, and the facet/tag
  lists). Trap worth knowing: System.Text.Json writes non-redeclared positional members
  first, so redeclaring *some* members of a record to add guards silently reorders its
  stored JSON — `Decoration` redeclares all five for that reason.
- A truncated embedding BLOB (length not a multiple of 4) threw an opaque
  `Buffer.BlockCopy` overrun; `ValueConversions.ToFloats` now rejects it by name.
- `ChatToolCallExecutor`'s single-argument binding path let a `JsonException` escape when
  the model sent malformed tool arguments, aborting the whole conversation. The
  multi-argument path already converted it to `ToolBindingException`; the single path now
  matches. (It also leaked a `JsonDocument` on every throw.)
- Solution-level `dotnet test` exited 1: `lionheart.csproj` carried `xunit`,
  `xunit.runner.visualstudio`, and `FluentAssertions` with zero usages, so VSTest treated
  the web project as a test project. Removed.

**Resolved by the owner 2026-07-20**, after being flagged by this audit:

- **Periods carry their offset.** `Instant` and `Interval` hold `DateTimeOffset`, so the offset
  in force when an entry was logged travels with it. A moment gets asked two different questions
  and each needs a different half: `Period.StartTimeUtc` is the absolute instant that the stamped
  `StartTime` column indexes (one timeline, correct ordering and windowing), while
  `Period.LocalDate` reads the moment at its captured offset and is what `Date` derives from.
  Deriving the day from UTC instead moves an evening session west of Greenwich to tomorrow —
  which would then join against the wrong night's sleep in the hidden-cost analytic. Capturing an
  offset rather than a time-zone id is deliberate: for an event that already happened the offset
  that applied at that instant is the whole answer, and it stays right across travel and DST with
  no rules lookup and nothing extra to ask the athlete. `ActiveTrainingSession.StartedAt` gets the
  same treatment so an in-progress session reports the same day it will once completed;
  `PerceivedEffortRatings.RecordedAt` is bookkeeping rather than training time and is simply
  normalized to UTC. Rows written before this still read: a bare `"...Z"` parses as `+00:00`.
- **Deleting a user deletes their `AnalyzableEntry` rows.** The read model is derived data and
  follows its source. `RemodelConfiguration` maps the `LionheartUser` relationship; migration
  `CascadeAnalyzableEntriesToUser` adds the FK.
- **Non-finite measures are refused at construction** — `Weight`, `Distance`,
  `ElevationGainMeters`, `TargetWeight`, `TargetDistance` via `Require.Finite`. Sign is
  deliberately *not* policed: net elevation gain is legitimately negative on a descent.
- **Failed tool calls now tell the model what went wrong**, per Ardalis status, instead of
  `System.String[]`.
- **JSON columns use the relaxed encoder**, so `sled pull+push` is stored as itself. Done now
  because there is no production data to rewrite. `ValueConversions.StorageJson` is public so
  the literal fixtures pin the bytes that actually reach storage rather than the ambient default.

Two footguns worth carrying forward, both discovered by the literal fixtures rather than by
reasoning:

- `[JsonIgnore]` does **not** inherit through an override — every `Period` subtype repeats it.
- System.Text.Json writes non-redeclared positional members **first**, so redeclaring only
  *some* members of a record to add a guard silently reorders its stored payload. `Decoration`,
  `LiftSet`, `DistanceTimeSet`, `TrailRunSet`, `LiftPrescription`, and `DistanceTimePrescription`
  therefore redeclare **all** their positional members, guarded or not.

**Still flagged — no decision yet:**

1. **Null elements inside stored lists survive as holes.** A `null` in a stored `Sets` array
   rehydrates as a null entry, so the first analytics pass touching `set.Source` throws far from
   the corrupt row. Deferred (owner, 2026-07-20) until the interpretation engine exists, since
   that is what will actually be producing these lists. Characterized by
   `Null_element_inside_a_set_list_currently_survives_as_a_hole`.
2. **Sign and upper bounds on measures.** Negative distance, weight, and duration persist
   happily. Characterized by `Negative_measures_are_accepted_by_design_for_now`.

**LLM test infrastructure.** No production seam was added: `ChatClient` accepts a
`Transport`, so tests build a real client over a fake `HttpMessageHandler` serving canned
OpenAI-format bodies. That is both the smallest change (none) and the highest fidelity —
it exercises real SDK request building, real response parsing, and
`ChatCompletionResultExtensions.ToResult`, which an app-owned interface fake would bypass
entirely. Covered: the tool-call loop (happy path, unknown tool, malformed arguments,
throwing tool, multi-round chains, empty and whitespace output, `length` and
`content_filter` finish reasons, refusals, LM Studio's HTTP-200-with-error body), and the
Athlete Context Card's narrative fallback driven through the real card service against
every LM Studio failure mode.

Interpretation-engine contracts are written but the engine does not exist: the parts the
domain can already answer run today (facet-axis closure against hallucinated axes, invalid
JSON, prose-wrapped JSON, confidence bounds), and eight `Skip`-marked specifications name
what must land for Parse/Resolve/Commit.

**LocalModel tests** (`Category=LocalModel`, excluded by default) read `LmStudio__*` through
the same `DotNetEnv` + environment-variable path as `Program.cs`, health-check the server,
try `lms server start`, and skip — never fail — with the failing key named. Commands are in
`lionheart.Tests/README.md`.

> **Verified against a real model 2026-07-20**: all four LocalModel tests pass end to end
> (`dotnet test --settings lionheart.localmodel.runsettings`, ~1m40s) — the terse-log parse
> returns schema-valid sets with a `Texted` source and in-range confidence, proposed facet axes
> stay inside the closed enum, and a registered tool really is called and its result returned.
> If `LmStudio__ApiKey` drifts from the token LM Studio is serving, both `/v1/models` and
> `/v1/chat/completions` answer HTTP 401 and the tests skip with that message rather than failing.

### ⬜ Step 3 — Interpretation engine

Generalize `Model/Ingestion/IngestionModels.cs` into the versioned
Parse → Resolve → Commit engine:

- Parse emits sets **and** a proposed `Decoration`; runs over Context text too.
- Resolve snaps facet values to `FacetValue` canonicals via embedding similarity
  (same pattern as the existing `UnresolvedReference`/`EntityMatch` flow).
- Commit writes the domain entity and upserts its `AnalyzableEntry` row.
- Stamp `DecoratorVersion` everywhere; engine must be re-runnable (freeform upgrades,
  re-decoration at newer versions).
- PR detection re-enters here: the legacy submit-attempt flow was deleted with the old
  model; the engine should evaluate completed lift sets against `PersonalRecords`.
- Chat tools for sessions/contexts also re-enter here (the legacy session/activity/
  program tools died with their services); the athlete-context-card coverage manifest
  currently tells the model there is no training drill-in tool — restore that line when
  the new tool exists.
- ~~First deliverable: a runtime JSON round-trip smoke test~~ — **done in step 2.5**:
  `lionheart.Tests` exercises save + rehydrate for every session lifecycle state, both
  context kinds, and `AnalyzableEntry` against real SQLite.

### ✅ Step 4 — Legacy removal (was: backfill; re-scoped by user 2026-07-19)

No backfill — old training data abandoned. Deleted outright: `Activity`,
legacy `TrainingSession`/`TrainingProgram`, `Movement` wrapper, `LiftSetEntry`/
`DTSetEntry` + the legacy `SetEntry` namespace (alias gotcha gone; `WeightUnit`/
`DistanceUnit` relocated to `Model/Training/Sets/Units.cs`), the ingestion
parse/commit services + endpoints + models (rebuilt properly in step 3), their
services/endpoints/DI registrations, and `PersonalRecord`'s lift-set source pointer +
attempt-submission machinery (read/revert surface kept). Migration
`DropLegacyTrainingModel` dropped the six legacy tables — applied to the dev DB.
Callers ported to the new model: injury ownership checks (movement links now mean
`MovementData` ids), equipment/movement-base in-use checks (vs `MovementDatas` — also
fixes a latent FK-crash), athlete-context-card session counts (completed sessions via
stamped `StartTime`).

**Known frontend fallout (accepted, fix rides the new UI work):** the TS client
regenerates from swagger at dev startup, so deleted endpoints vanish from
`ClientApp/src/lib/api/ApiClient.ts`; the Svelte stores/pages for sessions, training,
and ingestion reference removed methods and are broken until rebuilt against the new
endpoints (which don't exist yet — see open question 3).

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
4. **Do stored lists guard their elements?** (step 2.6). A null inside a stored `Sets` array
   rehydrates as a hole today. Deferred until the interpretation engine exists, since that is
   what will produce these lists — decide then whether a bad element rejects the row or is
   dropped, and which of the domain's ~8 list members get element guards.
5. **Sign and upper bounds on measures.** `Require.Finite` now rejects NaN and the infinities;
   negative distance, weight, and duration are still representable. Sign is not a blanket rule
   (net elevation gain is legitimately negative), so this needs per-measure judgement.
