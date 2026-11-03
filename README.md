# Lionheart

[![CI](https://github.com/chaserwacer/lionheart/actions/workflows/ci.yml/badge.svg)](https://github.com/chaserwacer/lionheart/actions/workflows/ci.yml)

A personal athletic-training platform: one place for training, recovery, wearable data, injuries and
subjective experience, built so a language model can reason over the whole picture rather than a slice
of it.

It is also, deliberately, a demonstration of how I think a modern C#/.NET and Svelte codebase should be
built. The design decisions are written down rather than implied — see [Architecture](#architecture).

> Product motivation and long-term vision: [`docs/project-vision.md`](docs/project-vision.md).

## Stack

| | |
|---|---|
| Backend | C# 14 · .NET 10 · EF Core 10 · SQLite |
| API | Ardalis.ApiEndpoints (one class per endpoint) · OpenAPI via Swashbuckle · Scalar |
| Frontend | SvelteKit 2 · **Svelte 5 runes** · Tailwind 4 · daisyUI 5 · installable PWA |
| Intelligence | Local LLM through LM Studio's OpenAI-compatible API — no cloud dependency, no per-token cost |
| Tests | xUnit — a hermetic suite plus a live-model suite behind an explicit gate |

## Architecture

Three ideas carry most of the design. Each has a document behind it, because a decision nobody wrote
down gets re-litigated by the next person to touch the code — including the next model.

### 1. The domain is made of algebraic data types

Impossible states are unrepresentable, and *fidelity is a first-class axis* — a terse log is a
genuinely smaller typed shape, never a god-schema full of nullable columns.

```csharp
public abstract record TrainingSession { /* id, user, notes, provenance, membership */ }

public sealed record PlannedTrainingSession(DateOnly PlannedFor, IReadOnlyList<SetPrescription> Prescriptions) : TrainingSession;
public sealed record ActiveTrainingSession(DateTimeOffset StartedAt, IReadOnlyList<SetEntry> Sets)            : TrainingSession;
public sealed record CompletedTrainingSession(IReadOnlyList<SetEntry> Sets, Period Period,
                                              EffortState Effort, DecorationState Decoration)                : TrainingSession, IAnalyzable;
public sealed record SkippedTrainingSession(DateOnly PlannedFor)                                             : TrainingSession;
```

A status enum plus nullable fields would let a *planned* session carry actuals. This cannot. The same
treatment runs through the model: `SetEntry` is `LiftSet | DistanceTimeSet | TrailRunSet | FreeformSet`,
a moment is `Instant | Interval`, and optional values carry encapsulated fill-state (`Rpe.Unrated`,
`EffortNotReported`, `Standalone`) rather than nulls.

Typed domain, JSON storage: polymorphic lists persist through value converters with pinned `$type`
discriminators, so the schema stays flexible while the code stays total.
→ [`docs/data-remodel.md`](docs/data-remodel.md)

### 2. Failure is a value, and composition is the point

`Result<T, Problem>` is owned rather than imported. The reason is concrete: the library it replaced had
no `Bind`, so every pipeline in the repo hand-unwrapped its own results. `Problem` is a closed ADT, so a
failure carries a *case* the code can match on instead of a string, and it becomes an HTTP status once,
at the edge — a domain refusal never learns what a 404 is.

The async combinator matrix is filled deliberately: both source shapes (`Result<T,E>` and
`Task<Result<T,E>>`) crossed with both selector shapes. A missing cell is what forces `await (await …)`
at a call site, which is the tell for an incomplete monad layer.

→ [`docs/api-and-composition.md`](docs/api-and-composition.md)

### 3. One interpretation engine turns text into structured training data

> "sled pull+push worked up to 235, 2 or 3 sets, shoulder felt tweaky"

**Parse → Resolve → Commit**, versioned and re-runnable. An LLM extracts sets and a proposed
annotation under a strict JSON schema; a resolver snaps movement names to the athlete's own vocabulary
and facet values to canonical ones by embedding similarity; the committer writes the domain entity plus
a denormalised row for retrieval. What it cannot break down stays a `FreeformSet` **by design** rather
than becoming a guessed lift — and the raw utterance survives every later enrichment.

Every annotation is stamped with the extractor version, so the same engine backfills its own past
output when the prompt or the model improves.

The surface for it is **Quick Log** (`/log`), which is the landing screen on a phone: one text
box, a microphone where the browser supports it, and a confirmation step that only ever asks about
the names it could not resolve.

→ [`docs/quick-log.md`](docs/quick-log.md)

## Testing

Two suites, one gate:

```bash
dotnet test --settings lionheart.runsettings            # hermetic — no network, no model
dotnet test --settings lionheart.localmodel.runsettings # live — needs LM Studio running
```

The hermetic suite covers domain invariants, JSON round-trips against **hand-written fixtures that pin
the on-disk bytes** (a round-trip test that uses the serializer as its own oracle cannot see a
serializer bug), EF behaviour against real in-memory SQLite, generated SQL rather than just results,
and the LLM tool-call loop through a fake transport under the real SDK.

It was written adversarially — a second pass aimed at breaking the first — and that pass found six real
defects, each of which now has a test that fails without its fix. They are listed in
[`docs/data-remodel.md`](docs/data-remodel.md), along with a **Traps** section recording non-obvious
failures already paid for. The most expensive: a reasoning model returns schema-conformant JSON in
`reasoning_content` and leaves `content` empty, so parses silently come back blank until
`reasoning_effort: "none"` is set.

## Running it

Requires .NET 10 SDK, Node 24, and [LM Studio](https://lmstudio.ai) for the chat and interpretation
features.

```bash
cp .env.example .env      # then set LmStudio__BaseUrl / ApiKey / Model / EmbeddingModel
dotnet run                # API + SPA proxy; regenerates the TypeScript client on startup
```

`ClientApp/src/lib/api/ApiClient.ts` is **generated** from the OpenAPI document each time the backend
starts. Never edit it by hand.

## Repository conventions

`CLAUDE.local.md` wires this repo to a set of engineering-standards skills, and the docs above are
written to be read by both people and coding agents — decisions, a decision log, traps, and open
questions with owners. Design intent that lives only in someone's head does not survive contact with an
agent, and increasingly does not survive contact with a team either.
