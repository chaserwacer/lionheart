# API boundary & functional composition — decision record

Companion to `docs/data-remodel.md`. That doc settles the domain and its storage; this one
settles how the domain crosses the HTTP boundary and how fallible work composes.

Same rules of engagement: **Decisions** outrank instinct. **Open** is the only place work is
allowed to be ambiguous — ask Chase rather than choosing.

## 1. Does the row-type persistence pattern apply at the API boundary?

**No.** Adversarially reviewed against `ef-core-persistence/references/modeling.md` before adopting.

The row-type pattern exists to work around limits that are **specific to EF**. Its four triggering
situations are:

1. an ADT that is not a single-discriminator partition — *EF gives one discriminator per hierarchy*
2. a store-computed state — *a view derives the column*
3. a bulk loader authoring the column list — *one author for COPY/DDL*
4. a mapped shape that must stay queryable where the domain shape is not — *converter-backed
   hierarchies do not answer type tests*

Every one is a property of a relational store. At the HTTP boundary there is no store, nothing
queries a DTO, and JSON nests discriminated unions natively. So none of the four transfer, and the
skill's own burden-of-proof line then rules **against** a mirrored layer here:

> Map the domain type directly by default. Any other row type is a DTO tax — pay it deliberately
> or delete it.

A full request/response mirror of the session hierarchy would be precisely that tax.

**But two different arguments — not the row-type argument — do justify request types:**

- **Authority.** The server owns `TrainingSessionId`, `UserId`, `Provenance`, `Decoration` and
  `DecoratorVersion`. Binding a create endpoint straight to `CompletedTrainingSession` lets a
  client assert `Provenance.User` on an AI-authored session, supply another user's id, or inject a
  fabricated `Decoration` that lands in the `AnalyzableEntry` projection and skews analytics.
  `Model/Require.cs` guards *values*; nothing guards *authority*. This is decisive.
- **The client cannot produce a domain record anyway.** `CompletedTrainingSession` requires a
  `DecorationState`, which only the interpretation engine can author. A direct bind is not merely
  unsafe, it is impossible.

Neither argument applies to **reads**. A response carrying the real discriminated shape is what
makes the generated TypeScript mirror the domain ADT — which is the whole payoff of having built it.

### Decision

| Direction | Shape |
|---|---|
| **Request** | Purpose-built command records, one per transition. Carry only what the client is authorized to state. Never mirrors of the domain. |
| **Response** | The domain records themselves, serialized polymorphically (`$type`), through one thin view projection whose only job is **hiding internals** (`SummaryText`, `DecoratorVersion`, raw embeddings) — never restating shape. |
| **Row-type layer at the API** | None. |

## 2. Transitions are the API, not shapes

`POST /api/training/sessions` (plans one) · `POST /api/training/sessions/{id}/start` ·
`/complete` · `/skip`. **Not** `PUT /api/training/sessions/{id}`.

A `PUT` accepting an arbitrary session shape lets a client drive Completed → Planned, re-opening
exactly the illegal states the sealed hierarchy closed. Transition endpoints make illegal
transitions unrepresentable at the boundary, for the same reason the domain is an ADT. It is also
what `ef-core-persistence` means by *commands operate on one aggregate*.

Both write paths are first class (owner call): the interpretation engine for text, and explicit
CRUD/transition endpoints for structured entry and editing.

## 3. `AwaitingConfirmation` resume — stateless

The client echoes the draft back with its disambiguation choices; the server re-resolves without
re-parsing. No `PendingInterpretation` table, no expiry policy, no cleanup job.

`ParsedInterpretation` is already fully serializable and already crosses the wire once — sending it
back costs a larger request body and nothing else. Server-side draft storage would buy resumability
across devices, which is not a requirement, in exchange for a table, a TTL, and a sweeper.

Confirmation payload: `rawName → (chosen entity id | create-new)`, per `UnresolvedReference`.

## 4. Functional core

`Ardalis.Result` is replaced by an owned `Result<T, TProblem>`. The reason is not taste: Ardalis
has **no bind**, so every pipeline in the repo hand-unwraps. `InterpretationEngine.InterpretAsync`
is three consecutive `if (!x.IsSuccess) return Result<T>.Error(new ErrorList([.. x.Errors]))`
blocks around what is genuinely a three-stage railway.

Built to `engineering-standards/references/csharp-functional.md`:

- `Result<T, TProblem>` — `abstract record`, `private protected` ctor, sealed cases, static
  `Success`/`Failure` factories usable through `using static`.
- The **full async matrix**. Both source shapes (`Result<T,E>`, `Task<Result<T,E>>`) × both
  selector shapes, for `Bind`/`Map`/`MapError`/`Match`. An empty cell forces `await (await …)` at
  the call site, which is the diagnostic for a missing overload.
- `Traverse` (fail-fast) for n-to-one. `TraverseAll` (accumulating) **only** where a real wire
  validation site wants every error at once.
- `Problem` as a closed ADT, not strings. Ardalis's `string[]` errors already produced a live
  defect: failed tool calls reported the literal text `System.String[]` to the model.
- Failure → HTTP mapping happens **once, at the endpoint edge**.

## Open — for Chase

Nothing below is decided.

1. ~~**`TreatWarningsAsErrors`.**~~ **Resolved — enabled.** It was held back because NuGet advisories
   on transitive packages would have failed the build for reasons no code change here could fix. That
   blocker is gone: the advisory count went 14 → 0 (see §12), so the flag is on and the build is at
   0 warnings, 0 errors.
2. **Project split** (`Lionheart.Core` / `.Infrastructure` / `.Api`). Deferred. `Functional/` is
   deliberately a top-level folder so the later split is a `git mv`, not a redesign.
3. ~~**NSwag's TypeScript for polymorphic `oneOf` responses** is unverified.~~ **Resolved — see §13.**
   It landed on the bad branch and was then fixed at the source rather than designed around.

## 5. Rulings made during implementation

Recorded because they were judgement calls, not transcription.

**Problem → HTTP.** `Upstream` is **502**, `Unexpected` is **500**. The Ardalis migration had
preserved 422 for both. 422 means a well-formed request that is semantically invalid, which neither
of them is — an upstream LLM failure is not the client's request being wrong.

**Two refusals were wearing the wrong case** purely to inherit that 422, and are reclassified by
what they actually are: Oura's "earliest date after end date" is `Invalid` (400); refusing to
withdraw a personal record that no longer stands is `Conflict` (409). A `Problem` ADT only pays
for itself if the case carries the meaning.

**`AddWellnessState` keeps its 200-vs-201 distinction.** The migration collapsed both branches to
201; updating an existing state is 200, creating one is 201. The old code was right. The other
status changes (the deletes, 200 → 204) are correct and stand — they now return what their
`[ProducesResponseType]` always claimed.

Oura sync was among those 204s and has since gone back to 200: it answers with the run's
per-collection tallies, which are the one thing about a sync a caller cannot learn any other way.
See `docs/oura-integration.md`.

**The Problem→HTTP map is pinned by a test.** It had been verified with a throwaway test that was
then deleted to hold a test count steady. Wrong trade: an unpinned mapping table is exactly the kind
of thing that drifts silently, and a rising test count is not a regression.

**Unwired combinators get deleted.** `Tap`/`TapError`/`Ensure`/`OrElse` shipped with no call sites.
They are wired where the endpoint work gives them an honest one (`Ensure` for ownership and state
guards, `TapError` for logging at the edge) and **deleted otherwise**. The standards call an unused
combinator a museum piece; four of them is the failure mode, not the vocabulary.

**No second `Problem` type, no `TraverseAll`.** Both were offered and both were declined on evidence:
the LLM and embedding failures all reduce to "an external dependency answered unusably" and no caller
branches further, so a second vocabulary would have bought four `MapError` seams and no information;
and the only accumulating call site turned out to be dead code.

## 6. Deferred, with reasons

- **The `CommandAsync` seam** (`references/orchestration.md`) wants `IDbContextFactory` and pooling.
  Real improvement, but a DI change touching every command — not landed alongside the endpoints that
  would use it. The Guard → Stage → Commit *phases* are followed regardless; only the shared seam is
  missing.
- **Async decoration on session completion.** `InterpretationCommitter` is create-only, so there is
  no update path for a session's `AnalyzableEntry` projection. Roadmap item, already owned.
- ~~**`[ValidateModel]` reimplementation.**~~ **Done** — see §12.

## 7. Follow-ups surfaced during implementation

Not done, and each one has a reason it was not done tonight.

- **`MovementResolver` writes `MovementBase`/`Equipment` rows through the `DbContext` directly.**
  They belong behind `FindOrCreate` on the movement/equipment services. Left alone because the
  resolver's constructor is pinned by existing tests and the change is wider than the wave that
  found it.
- **Touch reordering is impossible.** `svelte-dnd-action` is installed but unwired; reordering uses
  native HTML5 drag events, which do not fire on touch. On the iPhone this app exists to run on,
  movements and sessions cannot be reordered at all. Directly counter to the point of the PWA work.
- **`ClientApp/src/lib/components/training/index.ts`** is an unused barrel — everything imports by
  path. Delete.
- **`Microsoft.AspNetCore.Mvc.Testing`** sits in the web project so the test project inherits it. It
  belongs in `lionheart.Tests.csproj`.
- **`ILogger` appears nowhere in the codebase.** Noticed while looking for an honest `TapError` call
  site: there is no logging concern at all, so failures vanish silently once mapped to a status.
  Worth a deliberate decision rather than continued absence.

## 8. Two traps worth knowing before touching serialization

Both were found by running the code, not by reading it.

- **`ObjectResult.DeclaredType` must be set**, or MVC serializes the response by its *runtime* type
  and `$type` never reaches the client — silently. Every polymorphic response depends on it. Pinned
  by `ResponseDiscriminatorTests`.
- **System.Text.Json requires `$type` to be the first property on read.** A client that reorders keys
  while echoing a draft back breaks `/confirm` for no visible reason. `AllowOutOfOrderMetadataProperties`
  on the JSON options is the guard.

## 9. Verified against a live server

The endpoint suite is hermetic, so these were checked by driving a running instance — the mixed
route/body binding on transition routes in particular could not be tested any other way.

| Check | Result |
|---|---|
| `POST /sessions/planned` returns a discriminated body | `"$type":"planned"` ✓ |
| `POST /sessions/{id}/start` — route + body binding together | 200, becomes `"$type":"active"` ✓ |
| Skipping an **active** session (illegal transition) | 409 ✓ |
| Reading another user's session | 404, not 403 ✓ |

Two things this surfaced that tests did not:

- **A profile is a prerequisite for any training write.** `Sessions.UserId` has an FK to
  `LionheartUser`, and registering through the Identity API does not create one — so
  `POST /api/user/create-profile` must happen first or every session write is a 500. Worth an
  explicit refusal (`Problem.Conflict`, "profile not created") instead of an FK violation escaping
  as an unhandled exception.
- **`TrainingSession.CreatedAt` is a bare `DateTime`** while the remodel deliberately chose
  `DateTimeOffset` for time that matters. It empirically loses its UTC marker on a round trip —
  `...771956Z` when created, `...771956` after the transition reloaded it from SQLite, because
  SQLite hands back `DateTimeKind.Unspecified`. A client doing local-time math on that gets a
  silently wrong answer. Either make it `DateTimeOffset` like `Period`, or force the Kind on read.

## 10. Rulings on the endpoint wave

- **The session window matches each state on its own time-truth** (`StartTime` for completed,
  `StartedAt` for active, `PlannedFor` for planned) rather than only the stamped `StartTime` column.
  My spec said "window over the stamped StartTime column", which was underspecified: only completed
  sessions have one, so a calendar could never show a planned session. Widened deliberately.
- **Deleting a program that still has sessions is refused (409)**, not silently orphaned. Membership
  carries no FK, so nothing at the database level would have caught it. The spec was silent; refusing
  is right.
- **A lifecycle transition is `Remove` + `Add` on one key.** Changing state changes the TPH
  discriminator, which EF cannot update in place. Found empirically, pinned by
  `Persistence/LifecycleTransitionTests.cs`. Non-obvious enough to belong in Traps.
- **Sessions completed through `/complete` are `NotDecorated`** and therefore do not reach
  `AnalyzableEntry` — they are invisible to every analytic until the re-decoration path lands. Same
  for structured context creation. Characterized by `*_is_currently_undecorated_and_unprojected`
  tests; flip those assertions when that path arrives.

## 11. Reordering has no backend — rebuild, do not retrofit

An attempt to wire touch-capable drag-and-drop (`svelte-dnd-action` is installed but unused; native
HTML5 drag events do not fire on touch, so reordering is impossible on a phone) ran into the remodel:

`sessionStore.saveEdits` persisted order through `UpdateMovementOrderEndpointClient`. That client,
`UpdateMovementOrderRequest`, `MovementDTO` and the whole `Movement` wrapper are **gone** — deleted in
step 4 and absent from the regenerated client. A session is now one flat ordered `SetEntry` list
replaced wholesale through `PUT /sessions/{id}/sets`.

So "reorder movements" is not a broken feature; it is a feature whose domain concept was replaced.
Ordering now means **set-list ordering**, and the UI for it should be built against
`ReplaceTrainingSessionSetsEndpoint` rather than retrofitted onto the old model. Open for Chase:
confirm that reading, since it changes what the training screen is for.

Related: `ClientApp/tests/` is now empty (template stubs deleted, replaced by 55 real unit tests over
`lib/utils/training/`), so `playwright.config.ts`'s `testDir` no longer resolves and `npm test` fails
at the integration step. It had zero tests before either way. Drop the script or write a real e2e —
CI runs `npm run check`, not `npm test`, so nothing is red because of it today.


## 12. Dependency vulnerabilities: 14 advisories → 0

`dotnet list package --vulnerable --include-transitive` now reports clean for both projects. It
started at 14 unique advisories, four of them critical. Three steps got there:

1. **Seven dead packages removed** — SemanticKernel (+ its OpenAI connector), ModelContextProtocol
   (+ AspNetCore), two CommunityToolkit Aspire Ollama packages, `Microsoft.Extensions.AI.Ollama`,
   and EF Core InMemory. Every one had zero usages; SemanticKernel alone carried the critical
   advisory and dragged `MessagePack` in behind it.
2. **`Ardalis.ValidateModel` replaced in-repo** (`Endpoints/Filters/ValidateModelAttribute.cs`, used
   by 60 endpoints). The package targets netstandard2.0, so it pulled the whole ASP.NET Core **2.0**
   MVC tree along with it — four advisories for an `ActionFilterAttribute` of about ten lines.
   Behaviour is deliberately identical, so the wire contract did not change with the dependency.
3. **One transitive pin.** EF Core Sqlite 10.0.10 resolves `SQLitePCLRaw.lib.e_sqlite3` 2.1.11
   (GHSA-2m69-gcr7-jv3q, high). Pinned to 2.1.13 in `Directory.Build.props` rather than per-project,
   because a security pin only some projects carry is the one that gets missed. Remove it once EF
   Core resolves a patched version itself.

Also moved `Microsoft.AspNetCore.Mvc.Testing` out of the web project into the test project, where it
belongs — it was only in the web project so the test project would inherit it.

**Worth knowing:** `--` cannot appear inside an XML comment. A comment in `Directory.Build.props`
quoting the `dotnet list package` flags made the file unloadable, and MSBuild's symptom was not a
parse error but `TargetFramework` silently evaluating to empty, reported as
`NETSDK1013 / Invalid framework identifier ''` — which points at the framework, not the comment.


## 13. The OpenAPI document was lying about polymorphism

The design in §1 depends on the discriminated shape surviving to the client. It did not.

Swashbuckle emitted every sealed hierarchy as an empty object: `InterpretationOutcome` was `{}`,
`SetCommand` was `{}`, and the session/program views carried base properties only. NSwag can only
generate what the document describes, so the generated client had no derived types at all — and
`ParsedInterpretation.toJSON()` would have stripped `$type` off the echoed draft and broken
`/confirm` silently, which is the one path §3 exists to make cheap.

`AddSwaggerGen()` was bare. `Endpoints/OpenApi/PolymorphicSchemas.cs` now derives the OpenAPI
polymorphism **from the same `[JsonPolymorphic]` / `[JsonDerivedType]` attributes that drive
serialization** — reflecting over them for the subtype list, the discriminator property name, and
each discriminator value, walking up the hierarchy because the value is declared on the base rather
than the subtype. A second hand-maintained list would have been free to drift into a document that
lies about the wire format; this cannot.

The document now describes each base with a `discriminator` and a full `mapping`, and each derived
type with `allOf` — the standard OpenAPI 3.0 encoding — and the regenerated client contains
`Committed`, `AwaitingConfirmation`, `Instant`, `Interval`, `EffortReported`, `EffortNotReported`,
`ActiveSessionView`, `CompletedSessionView`, `LiftSetCommand` and the rest as real types.

## 14. An unreachable local model returned 500, not 502

The likeliest runtime condition for a local-model app — LM Studio not running, or misconfigured —
produced an opaque `"An internal server error occurred."`

`OpenAiCompletionResult.ToResult` only ever covered a response that *arrived*: it guards LM
Studio's HTTP-200-with-an-error-body, but a connection failure makes the SDK **throw**, and two of
the three transport call sites had no guard, so the throw reached the generic exception handler.
`EmbeddingGenerator` caught its own; the parser and chat service did not — the same logic missing
from two places, which is the extraction moment rather than a second copy.

`CompleteAsResultAsync` is now the one guarded way to call the model, converting a thrown
`ClientResultException`, `HttpRequestException` or timeout into `Problem.Upstream`. Verified live:
the same request that returned a bare 500 now returns **502** with a detail naming the actual cause.

## 15. Two rulings, since resolved

Both were put to Chase and both were approved as recommended; recorded here because the reasoning
outlives the decision.

1. ~~**`InjuryService` returns `Unauthorized` (401) for five ownership refusals.**~~ **Now `NotFound`.** It contradicts the
   house rule in §"Problem → HTTP" — a record belonging to another user is 404, so existence is not
   disclosed — and 401 is actively misleading: it tells the browser the session expired when the user
   is perfectly well authenticated and simply asked for someone else's row, which a client that
   redirects on 401 turns into a spurious logout. Pre-existing, not caused by this work, but it is a
   visible behaviour change on five routes, so it was not changed unilaterally.
   Five refusals across injuries, injury events and their session/movement links now answer 404.
2. ~~**`npm test` runs a Playwright step with no tests**~~ **Dropped.** (`ClientApp/tests/` is empty since the template
   stubs were replaced by 26 real unit tests). CI runs `npm run check`, so nothing is red today.
   `test:integration`, `playwright.config.ts` and the `@playwright/test` dependency are gone, and
   `test`/`test:unit` both run `vitest run` so neither can hang CI in watch mode. A configured-but-empty
   e2e suite reads as coverage that isn't there; write one when there is something to assert.

Also carried forward, lower priority: `stores.ts` is the last Svelte 4 `writable` in the codebase;
`lib/utils/training/formatters.ts` has no production consumers and duplicates live code (its 26 tests
should retarget `setFormat.ts` before it is deleted); and the interpretation path threads no
`CancellationToken`, so `/interpret` cannot be cancelled and still returns 500 for a profile-less
account — the one create path the new profile guard could not reach.
