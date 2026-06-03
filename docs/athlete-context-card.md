# Athlete Context Card (ACC) — Design Brief & Claude Code Kickoff Prompt

> Status: proposal / kickoff. This document (a) evaluates the "user profile card"
> idea, (b) sharpens it against current context-engineering practice, and (c)
> contains a ready-to-send, production-grade prompt for a Claude Code agent that
> will begin the work inside this repo.

---

## 1. Does the idea have merit?

Yes — strongly. The proposal is a concrete instance of the **agent memory /
context-engineering** pattern, which is the standard production answer to exactly
the problem described: a tool-calling chat model that re-retrieves and
re-analyzes the same source data every conversation burns tokens, latency, and
money, and produces inconsistent analysis across sessions.

Today in Lionheart (`Services/Chat/`), every conversation starts from a static,
200-token system prompt (`ChatConversationService.CreateChatConversationAsync`)
and the model must call tools (`GetWellnessStates`, `GetDailyOuraDataRange`,
`GetTrainingSessionsByDateRange`, `GetAllUserInjuries`, `GetPersonalRecords`, …)
to discover *who the athlete even is*. The tool results are persisted as
assistant messages (`LHChatToolCallResult`) to avoid re-fetching within a
conversation, but **across** conversations all of that retrieval + the model's
analysis of it is thrown away and repeated.

A persistent, evolving profile that is injected into the system message fixes
this. It is the right architecture. The improvements below make it *production
grade* rather than a naive "stuff a summary in the prompt" version.

---

## 2. The sharpened design

Name it the **Athlete Context Card (ACC)**. Core principles, refined from the
original idea using current memory-system practice:

### 2.1 Two memory tiers, different lifecycles

| Tier | Contents | Volatility | Refresh trigger |
|---|---|---|---|
| **Stable Profile** (semantic memory) | Identity, goals, training philosophy/modality, baseline biometrics (resting HR, HRV baseline, weight), equipment & movement preferences, chronic/recurring injury history, long-horizon (monthly/yearly) trends | Slow | Scheduled rollups + large structural changes |
| **Recent State** (working/episodic memory) | Today/this-week snapshot: latest wellness scores, **active** injuries + recent injury events, latest Oura readiness/sleep/resilience, acute training load & acute:chronic ratio, recent PRs, recent mood/mental-health trend | Fast | **Event-driven** — invalidated on each relevant data write/sync |

Keeping these separate is what makes the system both fresh *and* cheap: the slow
tier is computed rarely, the fast tier is cheap to recompute and small.

### 2.2 The card is a *derived materialized view*, never a source of truth

- Source tables (`WellnessState`, `Injury`, `DailyOuraData`, `TrainingSession`,
  `PersonalRecord`, …) remain canonical.
- The ACC is **regenerable from scratch** at any time → safe, auditable,
  idempotent, versioned. Store a schema/version stamp so cards can be rebuilt
  when the generator changes.
- Aggregation (counts, averages, acute:chronic load, PR deltas, sleep/HRV means)
  is **deterministic EF Core querying** — *not* the LLM's job. The LLM only
  writes the short *narrative* over already-computed numbers. This keeps numbers
  correct and bounds hallucination.

### 2.3 Rollups are computed offline by a cheap model, not at chat time

- Weekly / monthly / yearly summaries are produced by a background pipeline:
  deterministic aggregation → narrative generation by a **cheaper model**
  (e.g. a mini/Haiku-class model, separate `ChatClient`) → persisted.
- Chat-time cost becomes ~0 extra: assembling the card is a DB read + string
  render, no LLM call.

### 2.4 Tiered refresh / invalidation cadence

- **Recent State**: event-driven. When `AddWellnessStateAsync`, an injury event,
  or an Oura sync writes data, mark the user's recent-state slice stale (or
  recompute inline — it's cheap and deterministic).
- **Weekly**: rebuilt on a weekly schedule or when that week's data changes.
- **Monthly / Yearly**: scheduled.
- Every section carries an `as-of` timestamp + provenance + freshness so the
  model can reason about staleness (and respect the medical disclaimer when data
  is old/missing).

### 2.5 System-message assembly + prompt caching (the big token win)

- Render the ACC into a **bounded** text block (hard token budget, e.g.
  ~800–1500 tokens; truncate/elide lowest-priority sections first).
- Place the **Stable Profile as a stable prefix** of the system message so the
  provider's **prompt caching** can cache it across turns/conversations; append
  the smaller volatile **Recent State** after it. This compounds the savings:
  fewer tool calls *and* a cached prefix.
- Today the system message is one persisted `LHSystemChatMessage` per
  conversation. Decide deliberately: regenerate/refresh it at conversation start
  (and optionally per-message for Recent State) vs. keep it frozen. Recommended:
  Stable Profile cached + refreshed lazily; Recent State re-injected at
  conversation start.

### 2.6 The card actively *steers* tool calls (not just "points at" data)

The ACC reduces retrieval; it does not replace tools. But the bigger win is using
the card to make the model's tool-calling **more intelligent and deliberate**,
not just less frequent. The card carries an explicit, machine-readable **tool
routing** layer that turns "the model guesses what to fetch" into "the model
follows a decision policy":

1. **Coverage manifest** — for each domain, a compact line stating *what the card
   already contains and over what window*, so the model never re-fetches data it
   already has:
   - `wellness: last 7d summarized + today's scores present (as-of 2026-06-02)`
   - `oura: 7d readiness/sleep/HRV means present; per-night detail NOT loaded`
   - `training: 8-week load + acute:chronic present; per-session detail NOT loaded`
   - `injuries: 2 active (summarized); full event history NOT loaded`
   - `PRs: current PRs for 14 movements present; progression history NOT loaded`

2. **Drill-down pointers + the exact tool + arguments to use** — each "NOT loaded"
   item names the tool and the parameter shape that would retrieve it, so a needed
   call is unambiguous:
   - "For per-session detail call `GetTrainingSessionsByDateRange` with the date
     range in question."
   - "For injury event history call `GetUserInjuries` filtered to the injury."

3. **A decision policy in the system prompt** that consumes the manifest:
   - If the answer is fully supported by the card → **answer directly, no tools.**
   - If the question needs detail the manifest marks `NOT loaded` → call **exactly
     the named tool with the narrowest date range** that covers the question.
   - Never re-fetch a domain the manifest marks present-and-fresh for the same
     window; trust the card's deterministic numbers.
   - If card data is stale/missing (per its `as-of`) for a time-sensitive
     question → fetch; otherwise prefer the card.

This converts the card from passive context into an explicit **router**: the
model spends tool budget only on genuine drill-downs, with the right tool and
tight arguments on the first try, instead of exploratory round-trips. Tools stay
registered and available — the card changes *when and how precisely* they fire.

### 2.7 Context management across the conversation

The card changes the token profile of every turn, so the existing context budget
logic in `ChatCompletionService.HandleConversationHistory` (newest-first packing
against `MAX_INPUT_TOKENS`) must be made card-aware rather than left as-is:

- **One stable system block, not a growing one.** The Stable Profile is rendered
  once as the cached prefix; do not append a fresh card copy on every turn. Only
  the small Recent State is re-injected, and only when it has actually changed
  (compare its version/as-of stamp) — otherwise reuse the cached block so prompt
  caching keeps hitting.
- **Reserve the card's budget first.** Compute `cardTokens` and subtract it from
  `MAX_INPUT_TOKENS` *before* packing history, so the card can never be crowded
  out by old turns and history can never silently blow the window. The card has
  a hard cap (see 2.8); history fills what remains.
- **Prune history the card makes redundant.** Persisted `LHChatToolCallResult`
  messages are the largest, stalest payloads in a conversation. Once the card
  covers a domain/window, older raw tool-result messages for that same window are
  the first thing dropped from the packed context (they remain in the DB for
  audit; they just stop being re-sent). This is the single biggest in-conversation
  token saving and it composes with the cross-conversation saving from the card.
- **Summarize, don't truncate, long histories.** When history still exceeds its
  remaining budget, prefer rolling the oldest user/model turns into a short
  running synopsis (cheap-model, offline-style) over hard-cutting at a token
  boundary, so older context degrades gracefully instead of vanishing.
- **Keep the cacheable prefix byte-stable.** Anything that changes the prefix
  (even whitespace) busts the cache. Put all volatile content (Recent State,
  timestamps) *after* the stable prefix, never interleaved.

### 2.8 Safety, provenance, evaluation

- Carry confidence/recency so stale or sparse data is flagged; keep the existing
  no-medical-advice disclaimer posture.
- Add a lightweight eval: measure **tool calls per conversation** and
  **input tokens per conversation** before/after, and a spot-check that the card
  introduces no fabricated numbers (since numbers are deterministic, this should
  hold by construction).

### 2.9 What this unlocks later

Once the ACC exists, deeper questions need less retrieval because the framing
(baselines, trends, active issues) is already present — the model drills down
from an informed starting point rather than rebuilding context from zero.

---

## 3. Production-grade Claude Code kickoff prompt

Paste the block below to a Claude Code agent working in this repo. It is written
to maximize Claude Code performance and token efficiency: plan-first, grounded in
real file paths, reuse-existing-patterns, phased, with explicit token discipline.

---

```text
You are working in the Lionheart repo (.NET 9 / C# / EF Core / SQLite backend,
SvelteKit frontend). Branch: claude/lionheart-user-profiles-Ob5y0.

## Goal
Build an "Athlete Context Card" (ACC): a persistent, evolving, per-user profile
that is injected into the chat model's system message so the model has holistic
context up front and makes far fewer tool calls per conversation. Read
docs/athlete-context-card.md (this design brief) first — it defines the
two-tier model (Stable Profile vs Recent State), the "derived materialized view"
principle, offline rollups, tiered invalidation, prompt-caching layout, and
safety/eval requirements. Implement to that design.

## Phase 0 — Plan first (do NOT write code yet)
Enter plan mode. Read ONLY these files to ground yourself (do not scan the whole
repo): 
  - Services/Chat/ChatConversationService.cs   (where the system message is built)
  - Services/Chat/ChatCompletionService.cs     (completion + token-budget loop)
  - Services/Chat/ChatMessageService.cs         (per-message processing)
  - Model/Chat/ChatConversation.cs              (LHChatConversation, LHSystemChatMessage)
  - Model/Chat/Tools/ToolRegistry.cs + Model/Chat/Tools/ToolAttribute.cs (tool pattern)
  - Data/ModelContext.cs                        (DbSets, relationships, migrations)
  - Program.cs                                  (DI registration, ChatClient/model config)
  - One representative ToolProvider service, e.g. Services/WellnessService.cs,
    and skim Services/OuraService.cs, Services/InjuryService.cs,
    Services/Training/TrainingSessionService.cs, Services/Training/PersonalRecordService.cs
    for the data shapes (DailyOuraData, WellnessState, Injury/InjuryEvent,
    TrainingSession, PersonalRecord).
Use targeted reads/greps; don't re-read files already in context. Then produce a
phased implementation plan and the data model, and STOP for my approval before
coding. Your plan must cover:
  1. Persistence: new entities (e.g. AthleteContextCard with a StableProfile
     section + RecentState section + per-section as-of/provenance/version),
     DbSet additions in ModelContext, and an EF Core migration following the
     existing migration naming/style in Migrations/.
  2. Generation pipeline: a service (follow the existing
     interface + Result<T> [Ardalis] + constructor-injection + service-per-file
     conventions) that (a) deterministically aggregates source data via EF Core
     for weekly/monthly/yearly rollups and Recent State, and (b) calls a CHEAP
     model for the narrative only. Register a second OpenAI ChatClient for the
     cheap model in Program.cs rather than reusing the gpt-5.2 client.
  3. Refresh/invalidation: event-driven Recent State updates hooked into the
     existing write paths (AddWellnessStateAsync, injury event creation, Oura
     sync), plus scheduled rollups (propose IHostedService/BackgroundService).
     Keep aggregation deterministic; never recompute numbers with the LLM.
  4. System-message assembly + INTEGRATION into the live chat flow: render the
     ACC into a bounded, token-budgeted block; place Stable Profile as a
     cacheable stable prefix and Recent State after it. Specify exactly how this
     wires into the existing path — ChatConversationService builds the
     LHSystemChatMessage today, and ChatMessageService.ProcessUserChatMessageAsync
     drives each turn. Decide and justify: Stable Profile cached + refreshed
     lazily, Recent State re-injected at conversation start (and only when its
     version/as-of changed). Confirm/enable provider prompt caching for the
     stable prefix and keep that prefix byte-stable (all volatile content after
     it). This is a first-class deliverable, not an afterthought.
  5. Tool-call STEERING (the card must make tool use more intelligent, not just
     rarer): embed a machine-readable "coverage manifest" in the card (what each
     domain contains + over what window + as-of), plus drill-down pointers naming
     the exact tool + argument shape for anything NOT loaded. Add a decision
     policy to the system prompt: answer directly when the card suffices; when
     detail is needed, call exactly the named tool with the narrowest date range;
     never re-fetch a domain the manifest marks present-and-fresh. Keep all
     existing tools registered. Show before/after expected tool-call behavior on
     2-3 example questions in the plan.
  5b. Context management across the conversation: make
     ChatCompletionService.HandleConversationHistory card-aware — reserve the
     card's token budget before packing history; drop persisted
     LHChatToolCallResult messages that the card now makes redundant (keep them in
     the DB, just stop re-sending); prefer summarizing the oldest turns over hard
     truncation. State the new budget math explicitly (card reserved first, then
     history fills MAX_INPUT_TOKENS minus card).
  6. Token budget + safety: hard cap the card size with priority-ordered
     truncation; carry recency/provenance; preserve the no-medical-advice posture.
  7. Eval/observability: log tool-calls-per-conversation and input-tokens-per-
     conversation so the before/after win is measurable.

## Implementation constraints (follow existing conventions exactly)
  - C#: services expose an interface, return Ardalis Result<T>, use primary/
    constructor DI, one service per file, LH-prefixed chat types, existing
    namespaces. Match nullable + naming style of surrounding code.
  - Tools: the [ToolProvider]/[Tool] reflection registry auto-discovers tools;
    if you add tools, follow that pattern. Don't break ToolRegistryBuilder.
  - DB: add DbSets + relationships in ModelContext.OnModelCreating and generate a
    migration (dotnet ef migrations add <Name>) matching existing files. Do not
    hand-edit the model snapshot.
  - Frontend TS client is source-generated (Model/TsClientGen) — don't hand-write
    generated clients.
  - Build with the project's normal commands (dotnet build / dotnet ef ...);
    discover the exact test command before relying on it. Verify the build.

## Ways of working (token efficiency + Claude Code performance)
  - Plan-first, then implement in small, independently reviewable phases; pause
    between phases. Prefer Phase 1 = persistence + migration, Phase 2 = generation
    pipeline, Phase 3 = system-message assembly + caching + tool-routing manifest
    (the integration), Phase 4 = card-aware context management in
    ChatCompletionService (budget reservation + redundant-tool-result pruning),
    Phase 5 = invalidation + scheduling, Phase 6 = eval/observability (tool-calls
    and input-tokens per conversation, before/after).
  - Make independent tool calls in parallel; read only what you need; don't
    re-read unchanged files; prefer precise greps over broad scans.
  - Keep each commit focused with a clear message. Develop on
    claude/lionheart-user-profiles-Ob5y0; push with git push -u origin <branch>.
    Do NOT open a pull request unless asked.
  - Do not invent data fields the source models don't have — ground every card
    field in real entities (WellnessState scores, DailyOuraData readiness/sleep/
    resilience/activity, Injury/InjuryEvent, TrainingSession load, PersonalRecord).

Begin with Phase 0 and present the plan.
```

---

## 3a. Implementation status (integrated)

The ACC is implemented and wired into the live chat flow on
`claude/lionheart-user-profiles-Ob5y0`:

- **Entity / persistence** — `Model/Profile/AthleteContextCard.cs` (one derived
  card per user: Stable Profile + Recent State + coverage manifest, each with
  version + as-of stamps and a generator version for rebuilds). Registered in
  `Data/ModelContext.cs` with a unique index on `UserID`, and created by the EF
  migration `Migrations/20260603000000_AddAthleteContextCard.cs`.
- **Generation pipeline** — `Services/Profile/AthleteContextCardService.cs`:
  deterministic EF Core aggregation for both tiers. The **Stable Profile** carries
  identity, 12-month biometric baselines (resting HR, HRV, readiness, sleep),
  training cadence + history span, dominant training modality (most-trained
  movements), a concrete strength profile (strongest current lifts), chronic
  injury history, and a 90d-vs-prior-90d trajectory. The **Recent State** carries
  the latest wellness entry + 7d average + week-over-week trend, 7d Oura means +
  trend + latest night, acute:chronic load with an interpretation flag and 7d
  perceived-difficulty (RPE), PRs set in the last 30 days, and active injuries
  with last pain + recency. A **best-effort one-sentence narrative** is generated
  per tier; on any failure it falls back to the deterministic text — numbers never
  come from the LLM.
- **Single frontier model** — every AI use (flagship chat completions *and* the
  card narratives) runs through the one `ChatClient` (`gpt-5.2`) registered in
  `Program.cs`. There is no separate/cheap model.
- **Integration** — `ChatConversationService` renders the card into the system
  message (byte-stable cacheable prefix = instructions + profile + tool-routing
  policy; volatile manifest + Recent State appended after), with a static fallback
  (`RenderFallbackSystemMessage`) on any failure.
- **Tool steering** — the rendered system message carries a coverage manifest
  ("what's loaded + over what window + which tool to call for the rest") whose
  tool names match the registered tools, plus an explicit decision policy.
- **Context management** — `ChatCompletionService.HandleConversationHistory`
  reserves the card's budget first, packs user/model turns by priority, then
  fills remaining budget with tool-result payloads (dropped first, kept in DB).
- **Event-driven invalidation** — centralized via
  `IAthleteContextCardService.MarkRecentStateStaleAsync(userId)` and wired into
  every relevant write path: wellness (`AddWellnessStateAsync`), injuries
  (create/update injury + create injury event), and Oura sync (`SyncOuraAPI`).
  Marking is batched into the caller's `SaveChanges` (no extra round trip); the
  tier is rebuilt lazily on the next read (6h max age, or generator-version bump).

### Build & migration note (no .NET SDK in the web container)

This environment has no `dotnet` CLI, so the migration was hand-authored to match
EF's output (`Up`/`Down` + `.Designer.cs` + an updated
`ModelContextModelSnapshot.cs`, kept mutually consistent). Apply it locally:

```
dotnet build
dotnet ef database update
```

If you prefer EF to regenerate it, delete the three
`*_AddAthleteContextCard*`/snapshot edits and run
`dotnet ef migrations add AddAthleteContextCard` — the resulting schema is
identical.

### Remaining follow-ups (later phases)

- Scheduled weekly/monthly/yearly rollups via an `IHostedService`/`BackgroundService`
  (the lazy 6h refresh covers freshness today).
- Eval/observability: log tool-calls and input-tokens per conversation (before/after).
- "Summarize the oldest turns" history compaction (current pass drops tool
  results first; turn-summarization is the next refinement).

---

## 4. Why this is the token-efficient choice

- **Per-conversation:** static 200-token prompt + N tool-call round-trips (each a
  full model invocation over growing context) → bounded card in a **cached**
  prefix + near-zero tool calls for baseline context.
- **Cost is moved off the hot path:** the card is a DB read + string render at
  chat time; the only LLM cost is two short, best-effort narrative calls during a
  (re)build, amortized across every conversation that reuses the card.
- **Numbers stay correct for free:** deterministic aggregation means the
  expensive model never has to (re)compute metrics, only reason over them.
