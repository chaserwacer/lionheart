# Lionheart Data Remodel

Single source of truth for the training-data remodel, and the working context for whoever —
human or model — picks the work up next.

**How to use this document.**

| Section | What it is | How to treat it |
|---|---|---|
| Motivation → Migration & backfill | The intended shape of the domain | Settled. Build against it; do not re-litigate |
| Decision log | What was decided, and when | Outranks your instinct. If you disagree, raise it, don't quietly diverge |
| Status & roadmap | What has actually landed, with commits | **Update it as steps land** — standing instruction, not a courtesy |
| Traps | Non-obvious failures already paid for | Read before debugging anything that "should obviously work" |
| Open questions | The only place work is allowed to be ambiguous | If you need an answer that is not here, ask Chase rather than choosing |

Where the code lives: domain in `Model/`, persistence in `Data/`, the interpretation engine in
`Services/Interpretation/` + `Services/Embedding/`, tests in `lionheart.Tests/`
(see its README for how to run the live-model ones).

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
public abstract record Period;                                    // holds DateTimeOffset
public sealed record Instant(DateTimeOffset At) : Period;
public sealed record Interval(DateTimeOffset Start, DateTimeOffset End) : Period;
```

A moment answers two different questions and each needs a different half of it.
`Period.StartTimeUtc` is the absolute instant — one timeline, what the stamped `StartTime`
column indexes and what windows and ordering use. `Period.LocalDate` reads the moment at the
offset it was *logged* at, and is what a session's `Date` derives from. Deriving the day from
UTC instead moves an evening session west of Greenwich to tomorrow, which would then join
against the wrong night's sleep in the hidden-cost analytic. An offset rather than a time-zone
id is deliberate: for an event that already happened, the offset that applied at that instant
is the whole answer, and it survives travel and DST with no rules lookup.

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

`FacetAxis` and `Valence` are enforced closed at construction (`Model/Require.cs`), so a
corrupt row or a hallucinating model cannot widen the query surface the MCP tool depends on.

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
`MuscleGroup`, `PersonalRecord` — the last of which has since been remodelled outright rather
than extended, and its detection built (step 7 below).

## Out of scope (unchanged for now)

Chat, Profile, Wellness, Injury.

Oura was out of scope for the remodel and has since been reworked on its own terms — ten
collections, a new model and an import of the athlete's archived history. It reuses this domain's
`Period` for events and stamps `StartTime` the same way, but it does **not** feed
`AnalyzableEntry`, and `docs/oura-integration.md` carries the reasoning for that along with
everything else about it.

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
| Measure validity | Non-finite rejected at construction; sign deliberately unpoliced, since net elevation gain is legitimately negative (2026-07-20) |
| Closed enums at the boundary | `FacetAxis`/`Valence` guarded in constructors, so deserialization, `with`, and app code are all covered (2026-07-20) |
| JSON encoder | Relaxed encoder on storage options — payloads never reach HTML, and readability of the SQLite file wins (2026-07-20) |
| Embedding provider | Local LM Studio (2026-07-20). Still the local server once chat moved to Anthropic, since Anthropic has no embeddings endpoint |
| Entity matching | Movements/equipment stay **lexical**; only facet values resolve semantically — a deliberate narrowing of "snap entities and facet values" (2026-07-20) |
| Live-test gating | One mechanism: `Category=LocalModel` trait selects, `[LmStudioFact]` skips with the failing key named (2026-07-20) |
| Embedding provider | An OpenAI-compatible server, configured independently of chat under `Embedding__*` — Anthropic serves no embeddings endpoint, so there is deliberately no provider choice here. Model pinned in `Embedding__Model`; dimension mismatch is a hard error, never a silent rescore |
| Entity vs facet resolution | Facets snap semantically (embeddings); movements/equipment stay lexical — no embedding column on those tables, and exact names dominate in practice |
| Strong history | The athlete's Strong export imports as completed sessions (owner request 2026-11-03). Distinct from the abandoned legacy Lionheart data, which stays abandoned. Warm-ups become freeform sets; drop and failure sets are lifts with their kind in the notes — no set-kind field until the owner decides one is wanted |

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

Second pass over step 2.5, written to break it rather than confirm it. The suite went from 76
tests to ~293 hermetic plus 10 live.

**What it closed.** Hand-written JSON literal fixtures pinning the on-disk bytes (the
round-trip tests used the serializer as their own oracle, so a serializer-level bug was
invisible to them); EF change detection through the value converters, including proof the
vector comparer's cheap `v.Length` hash is safe because its equality is exact; DateTime
fidelity; malformed rows planted by raw SQL and read back through EF; relational edges;
query translation asserted against generated SQL rather than results; hostile values; and
concurrent stamping across two contexts on one connection.

**Defects found and fixed** (each has a test that fails without the fix):

- Derived members were serialized into stored JSON — `Period.StartTime`, `Interval.Duration`,
  `DecorationState.IsDecorated` — contradicting "derived, never stored beside its source".
- `FacetAxis`/`Valence` ordinals outside their enums, and nulls in non-nullable members,
  rehydrated silently. Guarded in constructors via `Model/Require.cs`.
- A truncated embedding BLOB threw an opaque `Buffer.BlockCopy` overrun; `ToFloats` now
  rejects it by name.
- `ChatToolCallExecutor` aborted the whole conversation on malformed tool arguments: the
  single-argument path let `JsonException` escape where the multi-argument path already
  converted it. It also leaked a `JsonDocument` on every throw.
- Failed tool calls reported `Errors.ToString()` to the model — the literal text
  `System.String[]` — instead of why they failed.
- Solution-level `dotnet test` exited 1: `lionheart.csproj` carried `xunit` and
  `FluentAssertions` with zero usages, so VSTest treated the web project as a test project.

**Owner decisions landed the same pass:** `Period` carries offsets (see Domain model);
`AnalyzableEntry` cascades with its user (migration `CascadeAnalyzableEntriesToUser`);
`Require.Finite` rejects non-finite measures; storage JSON uses the relaxed encoder, with
`ValueConversions.StorageJson` public so the literal fixtures pin the bytes that actually
reach storage.

**Mocked-LLM tests fake the transport, not a seam.** Tests build a real SDK client over a fake
`HttpMessageHandler` serving canned wire bodies, which keeps real request building, real response
parsing, and `OpenAiCompletionResult.ToResult` in the path — all of which an interface fake would
bypass. Covers the tool-call loop end to end: unknown tool, malformed arguments, throwing tool,
multi-round chains, empty and whitespace output, `length`/`content_filter` finish reasons,
refusals, and LM Studio's HTTP-200-with-error-body.

A later step added an app-owned `IChatModel` port so chat can run on Anthropic in production
(`Services/Llm/`), which does **not** supersede the reasoning above: both providers are still
faked at the wire, with their own SDK in the path, and each fake hands out an `IChatModel` so the
consumer under test is wired exactly as the app wires it. What the port bought was one tool loop
instead of two, not a test double.

### 🟨 Step 3 — Interpretation engine (core landed; chat tools for sessions/contexts remain)

**Embedding infrastructure (new).** Nothing in the repo generated vectors before this step
— the deleted ingestion pipeline resolved names with Levenshtein only. Embeddings now come
from LM Studio's OpenAI-compatible endpoint (the same local server that serves chat, user
call 2026-07-20), wired as an `EmbeddingClient` singleton behind `IEmbeddingGenerator`
(`Services/Embedding/`). The model is pinned in `Embedding__Model`; because vectors
from different models are incomparable, `Vectors.CosineSimilarity` returns an **error** on a
dimension mismatch rather than scoring it, so a model swap surfaces as a loud "must be
recomputed" instead of silently corrupting resolution.

**Engine (`Services/Interpretation/`), one stage per file, orchestrated by
`InterpretationEngine`:**

- `InterpretationParser` — LLM call under a strict JSON schema; emits drafts **and** a
  proposed `Decoration`, over sessions and both context kinds. A set it can't break down
  stays `DraftFreeformSet` by design rather than becoming a guessed lift.
- `MovementResolver` — names → `MovementData` ids, reusing `FindOrCreateMovementDataAsync`.
  Unmatched names come back as `UnresolvedReference` + ranked candidates.
- `FacetResolver` — proposed facet values → canonical `FacetValue`s: literal/alias match
  first, then embedding snap above a 0.82 cosine threshold (accruing the raw value as an
  alias), else admit a new canonical. Resolves the whole run in **one** embedding batch.
- `InterpretationResolver` — assembles real domain records; stamps `DecoratorVersion`.
- `InterpretationCommitter` — writes entity + `AnalyzableEntry` in one save. The
  projection's facets are the **union** of entry-level and per-set facets, since movement
  patterns only ever exist at set level and step 5 filters on them.

`Model/Interpretation/InterpretationModels.cs` holds the draft layer (it exists because
parsed text carries movement *names*, not ids) and the outcome hierarchies:
`Resolved | Ambiguous` for the resolve stage, `Committed | AwaitingConfirmation` for a run.

**Reasoning-model trap (found by running it, 2026-07-20).** The configured chat model
(`qwen/qwen3.6-35b-a3b`) is a reasoning model: while it thinks, LM Studio returns the
schema-conformant JSON in `reasoning_content` and leaves `content` **empty**, which the
parser sees as an empty response. Neither `chat_template_kwargs.enable_thinking=false` nor
`/no_think` changes this; **`reasoning_effort: "none"` does**, and that is what the LM Studio
provider sets when a request asks for `ReasoningDepth.Minimal` (SDK property still experimental —
`OPENAI001` suppressed at that one site). The parser also now fails with a Result error naming
this cause instead of indexing an empty `Content` collection. Extraction gains nothing from a
reasoning pass, so this costs nothing.

**The same trap on Anthropic, in a different spelling.** On Claude Opus 5.5 thinking is always on
and cannot be switched off at all — `ThinkingConfigDisabled` and a token budget are both a 400 —
so a turn routinely carries thinking blocks that are not the answer. The Anthropic provider reads
only the text blocks, and asks for `OutputConfig.Effort = Low` for the same
`ReasoningDepth.Minimal`. The depth knob is nested under `OutputConfig`, not top-level.

Tests: the hermetic set (fakes, needs nothing running) covers cosine + dimension mismatch,
facet literal/alias/snap/admit paths, alias accrual, two-near-values-one-pass key-collision
avoidance, stale-vocabulary and embedding-failure propagation, movement
match/candidate/dedup/cross-user isolation, no-create-on-ambiguous, and the draft→domain
mapping. Ten **live** tests (`[LmStudioFact]`, `Category=LocalModel`) are the only thing that
can catch the model breaking schema compliance — run them after touching the prompt, the
schema, or the configured model. All ten verified green against qwen3 +
`text-embedding-qwen3-embedding-4b` (2560-dim), including the full engine committing a session
with its projection and an unknown movement returning `AwaitingConfirmation` with nothing
written.

**Deliberate narrowings, and what is left:**

- **Entity matching stayed lexical.** The doc line "snap entities *and* facet values via
  embedding similarity" is implemented semantically for facets only: `MovementBase` and
  `Equipment` carry no embedding column, and adding one is a migration this step didn't
  need. Exercise names the athlete already uses match exactly far more often than they
  paraphrase. Revisit if unresolved-reference prompts prove noisy.
- **Commit is create-only.** Re-decoration/backfill (`WHERE DecoratorVersion < current`)
  needs an update path on both the entity and its projection; the engine is versioned and
  re-runnable in shape, but that entry point does not exist yet.
- **The confirmation round-trip is one-directional.** `AwaitingConfirmation` carries the
  draft back so the user's choices can be re-resolved without re-parsing, but the resume
  path lands with the endpoints (open question 3).
- **Record detection** landed in step 7 below, and the committer stages records for every
  completed session it writes.
- **Chat tools** for sessions/contexts still to come; when the training drill-in tool
  exists, restore the athlete-context-card coverage manifest line that currently says
  there is none.
- ~~First deliverable: a runtime JSON round-trip smoke test~~ — done in step 2.5.

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

### ✅ Step 7 — Personal records, remodelled and detected

The deferral recorded above ("PR *detection* returns with the interpretation engine", 2026-07-20)
is lifted. The surviving pre-remodel entity was **remodelled rather than extended**: it was a
mutable class with data annotations, a two-value `Strength | Volume` enum, and weight-unit-reps
always present, which cannot express a distance record without becoming an enum beside a bag of
nullable columns — the shape these standards exist to prevent.

**Domain** (`Model/Training/PersonalRecords/`):

- `PersonalRecord` — a `sealed class`, the one entity in this domain that is not a record, because
  its standing genuinely changes. Everything it holds is immutable.
- `RecordSubject` — `Movement(id)` or `TrailRunning`, persisted as a Guid with `Guid.Empty` for
  trail running, the same fill-state-to-scalar shape `ProgramMembership` takes.
- `Achievement` — the kind fused with its measurement, seven sealed cases, so a heaviest-set
  record cannot hold a distance. Each case answers `Kind`, `HeldIn` and `Rank`.
- `RecordKind` — which question: `HeaviestSet`, `BestSetVolume`, `RepMax(reps)` for two to
  twelve reps, `BestSessionVolume`, `LongestDistance`, `FastestTime(meters)`, `BestPace`. A rep
  max's achievement holds its rep count beside the set holding it, since a set of seven holds the
  six-rep max too; `RecordKind.RepMax.HeldBy` is where a set's reps become the counts it holds.
- `RecordKey` — what makes two records the same record: the subject, the kind, and the weight
  unit where there is one. Distances carry none, since they rank in meters.
- `LiftPerformance`, `SessionVolume`, `DistanceEffort` — the measurements, carrying the derived
  figures (volume, estimated one-rep max, pace, distance bracket) as `[JsonIgnore]`d members so
  none of them reaches storage.
- `RecordStanding` — `Current | Beaten(at) | Retracted(at)`. Three states rather than `IsActive`,
  because a record the athlete beat and one they withdrew read nothing alike.
- `RecordLineage` — `FirstOfItsKind | Replaced(id, achievedAt)`.

**Detection** happens when a session's sets become final, which is the one moment they stop
changing: `TrainingSessionService.LogCompletedAsync`, `.CompleteAsync`, and
`InterpretationCommitter` (a session logged as text sets the same records one logged through the
form does). `PersonalRecordDetector.StageAsync` stages into the caller's unit of work rather than
saving, so the session and its records land in one save or neither does. Nothing in detection is a
refusal: the session has already been accepted, so a candidate on a movement that is not the
athlete's simply raises no record.

**One record per identity per session, and what stands is read once per commit.** A session whose
third, fourth and fifth sets each beat the standing record adds one link, holding the best of
them: the sets in between never stood as records, so a row each would read as several records set
in the same instant with no way to tell which held. The detector therefore takes *every* session a
caller is about to save, not one at a time — one utterance can yield two sessions on the same
movement, and a detector asked session by session reads what stands before each, a read that
cannot see the record the previous session staged but has not saved. Both would be written
standing. Sessions in a batch are processed oldest first, so two of them on one movement chain the
way they happened.

**Settled rules worth not relitigating:**

| Rule | Why |
|---|---|
| Weights never convert | 225 lb is the larger number and the lighter lift. A set in a unit the record is not held in does not beat it, and both clients compare the same way |
| The unit is part of a record's identity | Which is what stops a movement locking to the first unit logged. Without it a kilograms lift could neither beat a pounds record nor become one, so a unit switch would end that movement's progression for good. A movement holds one record per kind per unit, each set and beaten within its own — the same keying the iOS trends chart already uses so a unit switch starts a new line |
| Distances do convert | The four units are exact definitions of one quantity and two are metric spellings of one scale; refusing to relate 5 km to 5000 m would be a defect |
| A set holds the rep max at its own count and every count below it | Not inference: a set of seven *contains* a set of six — the first six reps were actually performed. It is how lifters speak ("hit 105 for six, so that's a five-rep PR too"), and it is what makes the grid read as a record table, weight never rising as reps rise; with exact counts a seven-rep max could sit heavier than the six. Within a session a count's best is the heaviest set with at least that many reps, the longer set winning a tie as it does for the heaviest set. The estimated one-rep max is the opposite — a single nobody lifted — so it stays derived and labelled. Carry-down never crosses units |
| A carried record says which set it came from | The achievement keeps the rep count and the set as performed apart, and the wire sends both (`reps`, `setReps`): a five-rep max from a clean five and one from the front of an eight are the same record and different stories |
| The one-rep max is the heaviest set | Every set contains a single, so with carry-down a rep max at one is the heaviest set by definition. Rep maxes run from two; the grid's first cell reads the heaviest-set record, and `repMax` with a bracket of one is refused toward `heaviestSet` |
| The rep-max grid stops at twelve | Past that the number describes work capacity, which the volume records answer. A longer set still holds every rep max from two to twelve, because a set of twenty contains a set of twelve |
| Trail running is its own subject | A trail set names no movement to hang a record off, and a route's distance is whatever the route was where an interval's is a number the athlete chose, so a season of trail runs ranks against itself rather than against the treadmill. Terrain is not part of the identity: two routes of one distance and different climb share a record, because climb is not a bracket anyone logs deliberately |
| Best pace is a record, the pace number is not stored | The set holding the best pace is a different set from the longest or the fastest, so it needs a row; the pace itself is a function of the distance and the time beside it |
| Zero load or zero reps raises nothing | A zero can never beat another zero, so admitting it plants a record that can only be equalled. Bodyweight-reps records are a kind this does not have |
| No unique index over the standing records | SQLite checks a unique index one statement at a time with no deferred form, so a hand-over's insert is refused while the row it replaces still stands. `PersonalRecord.Succeeding` writes both halves instead |

**Migration `PersonalRecordComprehensive`** copies the existing rows into the new shape rather than
shuffling columns, which the scaffolded version would have done. Applied to the dev database: all
33 rows survived — 17 as `heaviestSet` and 16 as `bestSetVolume`, 20 standing and 13 beaten, with
their chains intact. Nothing migrates as retracted, because the legacy flag could not tell a record
the athlete beat from one they withdrew.

**Migration `DateBeatenRecordsBySuccessor`** corrects the one thing that migration got wrong: it dated
each beaten record's end by the record's own date, when the date it stopped standing — its earliest
successor's — was available by following the lineage back. A follow-up rather than an edit, because
the first had already run against the dev database and a migration file must keep describing what
ran. It touches only rows still carrying that stamp, so it is a no-op on anything the detector has
written. Against the dev database it corrects 8 of the 13 beaten rows; the other 5 are branches the
legacy writer abandoned, which nothing points back at, and keep their own date. Not applied there
yet — nothing in the app migrates at startup, so it lands on the next `dotnet ef database update`.

### ✅ Strong history import

`dotnet run -- import-strong <strong-export.csv> --user <athlete-guid>`, packaged like the Oura
archive import (`Services/Strong/`). Each Strong workout becomes a `CompletedTrainingSession`
(`Provenance.User`, standalone, `NotDecorated`, effort not reported) over `Interval(start, start +
duration)`, the start placed at the machine's time zone as it applied on that date. Sets are
`LiftSet`s in pounds with Strong's RPE; set and workout notes, a non-default workout name, and which
sets were drop sets or taken to failure go into the session notes. Rest-timer rows are counted and
set aside — the domain has nowhere to hold a rest.

- **Set kinds without a schema change.** A warm-up is a `FreeformSet` holding its own words
  ("Warm-up — Bench Press (Barbell): 155 lb × 13"): it must not set a record, a freeform set raises
  none, and the stored session then reads exactly as the detector saw it. Drop and failure sets
  were performed at the weight stated, so they are lifts and count toward records.
- **Movements** come from `Services/Strong/strong-movements.json`, an athlete-editable map from
  every Strong exercise name to base / equipment / modifier, matched against the athlete's
  catalogue ignoring case and surrounding spaces. Judgement calls carry an `unsure` reason and are
  listed by every run that applies them. An unmapped name refuses the import before anything is
  written.
- **Re-runnable.** A session's id is a name-based (v5) UUID of the athlete and the workout's start,
  so a workout already held is recognised and skipped, and renaming it in Strong does not import it
  again. A session deleted here does come back on the next run, and a mapping corrected after a
  workout was imported does not reach it.
- **Records as if logged live.** The new sessions go through `PersonalRecordDetector` in one batch,
  oldest first. The import refuses — writing nothing — if a record already standing on an identity
  the new sessions contest was set after the earliest of them, since the replay could not then run
  in the order things happened.
- **All or nothing.** Movements, sessions and records commit in one transaction; any unreadable
  line refuses the whole export, naming every such line.

**Running it.** From the checkout whose `Data/lionheart.db` is meant — the connection string is
relative, so a run from a worktree writes that worktree's copy and reports success. The run prints
the time zone it read times in; the zone is baked into the session ids, so a wrong one cannot be put
right by running again. Against the athlete's database as of 2026-11-03 it refuses: ten records
standing from the legacy app, all stamped 2026-03-23 16:30:32 (the moment that data was loaded, not
when he lifted), duplicate the Strong workout of 2026-03-20 on five movements. Withdrawing one puts
back a predecessor stamped the same instant, so clearing them is fifteen withdrawals. Whether to
withdraw them, delete them, or have the import treat them otherwise is the owner's decision; nothing
is decided here.

### Traps — non-obvious failures already paid for

Read this before debugging something that "should obviously work".

- **Reasoning models return an empty `content`.** With thinking enabled, LM Studio routes the
  schema-conformant JSON to `reasoning_content` and leaves `content` empty. Neither
  `chat_template_kwargs.enable_thinking=false` nor `/no_think` changes it; **`reasoning_effort:
  "none"` does**, and that is what the LM Studio provider sends for `ReasoningDepth.Minimal`. If
  parses come back empty, look here first.
- **On Anthropic, thinking cannot be turned off at all.** `ThinkingConfigDisabled` and any token
  budget are both a 400 on Claude Opus 5.5, so every turn may carry thinking blocks. Read the
  **text** blocks only; concatenating the content union puts the model's reasoning in front of the
  athlete. Depth is `OutputConfig.Effort`, nested there rather than top-level.
- **Forcing a tool call to get JSON back is gone.** A `tool_choice` of `any` or `tool` returns a
  400 on Claude Opus 5.5, so structured output is `OutputConfig.Format = new JsonOutputFormat
  { Schema = ... }`. Assistant prefill is a 400 too. `Llm/AnthropicChatModelTests.cs` asserts the
  request carries neither.
- **Anthropic has no `.ToParam()`.** Echoing a turn back means rebuilding each content block as its
  `*Param` counterpart, and a thinking block's `Signature` must survive unchanged or the request is
  rejected. Every `tool_use` id needs exactly one matching `tool_result` in the follow-up, which is
  why a round's tool outcomes cross the port together rather than one at a time.
- **`[JsonIgnore]` does not inherit through an override.** Every `Period` subtype repeats it,
  or the derived member leaks back into stored JSON.
- **Redeclaring *some* positional record members reorders the stored JSON.** System.Text.Json
  writes non-redeclared positional members first. Records persisted as JSON that need a
  constructor guard (`Decoration`, the `SetEntry` and `SetPrescription` shapes) therefore
  redeclare **all** their members, guarded or not. `StorageFormatTests` is the tripwire.
- **EF writes Guid TEXT columns uppercase in SQLite**, and SQLite compares TEXT byte-for-byte.
  A lowercase literal in hand-written SQL matches nothing. Use `RemodelDatabase.SqlGuid`.
- **`ExecuteScalar` returns `DBNull.Value`, not null**, which is non-null in C# and will
  quietly satisfy an `Assert.NotNull` on an empty column. `RemodelDatabase.ExecuteScalar`
  normalizes it.
- **VSTest ANDs a `--filter` with the runsettings `TestCaseFilter`**, so
  `--filter "Category=LocalModel"` resolves to an empty set. Select a different settings file
  instead; see `lionheart.Tests/README.md`.
- **Live tests have one gate, with two halves.** The `Category=LocalModel` trait *selects*
  (keeping a plain run hermetic and network-free at discovery time); `[LmStudioFact]` +
  `SkipUnlessAvailable()` *skips* with the failing key named. Do not add a third mechanism, and
  never read `LmStudio__*` from the environment directly — `Support/LmStudioSettings.cs` is the
  one reader, so a missing key becomes a named skip rather than a `NullReferenceException`.

### Open questions

Numbered for reference; nothing here is decided. **Ask Chase rather than choosing.**

1. **DTO/request shapes and endpoints** for sessions, contexts, programs, and the engine —
   including where the `AwaitingConfirmation` resume path lands. **This is the blocking item:**
   the Svelte stores under `ClientApp/src/lib/stores/` call endpoints deleted in step 4 and stay
   broken until it is answered.
2. **Does the domain police measure sign and upper bounds?** `Require.Finite` rejects NaN and
   the infinities; negative distance, weight, and duration are still representable. Not a
   blanket rule — net elevation gain is legitimately negative — so it needs per-measure
   judgement. Characterized by `Negative_measures_are_accepted_by_design_for_now`.
3. **Do stored lists guard their elements?** A `null` inside a stored `Sets` array rehydrates
   as a hole, so the first analytics pass touching `set.Source` throws far from the corrupt row.
   Deferred until the engine's write paths settle. Decide whether a bad element rejects the row
   or is dropped, and which of the ~8 list members get guards. Characterized by
   `Null_element_inside_a_set_list_currently_survives_as_a_hole`.
4. **Is the 0.82 facet snap threshold right?** A reasonable starting point, **never tuned
   against real data**. Now that real 2560-dim vectors flow, worth revisiting once actual logs
   accumulate — too low silently merges distinct concepts, too high fragments the vocabulary.
5. **Parse quality beyond schema compliance is unmeasured.** The live tests assert structure —
   a session with sets, a context for a sleep note — not extraction accuracy across messy
   phrasing. Worth throwing real logs at it before endpoints are built on top.
6. **Three hand-rolled fill-states** (`DecorationState`, `EffortState`, `ProgramMembership`) —
   a generic `Option<T>` is deferred unless a fourth appears.
7. **De-"remodel"-ing identifiers** — `RemodelConfiguration` → `TrainingDomainConfiguration`
   and similar, now that the remodel is the model rather than a migration in flight. Offered
   twice, still unanswered. **Do not do it without asking.**
8. **Embedding-model changes force a full recompute.** `Vectors.CosineSimilarity` errors on a
   dimension mismatch rather than scoring it, so the failure is loud — but no recompute entry
   point exists yet. It arrives with the re-decoration path (step 3's remaining work).

### Next steps, in order

1. **Endpoints + DTOs** (open question 1), then rebuild the broken Svelte stores against them.
2. **Re-decoration / backfill entry point** — needs an update path on both the entity and its
   `AnalyzableEntry` projection; `InterpretationCommitter` is create-only today. Three skipped
   specs in `lionheart.Tests/Llm/InterpretationContractTests.cs` light up when this lands.
3. **Chat tools** for sessions/contexts; when the training drill-in tool exists, restore the
   athlete-context-card coverage-manifest line that currently says there is none.
4. **Step 5** balance ledger, then **step 6** retrieval primitive + MCP surface.
