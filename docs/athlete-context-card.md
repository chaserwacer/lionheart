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

### 2.6 The card must point at what it omits ("retrieval pointers")

The ACC reduces retrieval; it does not replace tools. Include compact pointers
("PR history exists for 14 movements; ask to drill in", "32 logged sessions in
the last 8 weeks") so the model knows *when a tool call is still warranted*
instead of guessing or hallucinating. Tools stay registered and available.

### 2.7 Safety, provenance, evaluation

- Carry confidence/recency so stale or sparse data is flagged; keep the existing
  no-medical-advice disclaimer posture.
- Add a lightweight eval: measure **tool calls per conversation** and
  **input tokens per conversation** before/after, and a spot-check that the card
  introduces no fabricated numbers (since numbers are deterministic, this should
  hold by construction).

### 2.8 What this unlocks later

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
  4. System-message assembly: render the ACC into a bounded, token-budgeted
     block; place Stable Profile as a cacheable stable prefix and Recent State
     after it. Decide how this integrates with the existing per-conversation
     LHSystemChatMessage (refresh-on-create + Recent-State re-inject vs frozen).
     Confirm/enable provider prompt caching for the stable prefix.
  5. Retrieval pointers: include compact "what exists / ask to drill in" hints so
     the model still knows when to call a tool. Keep all existing tools registered.
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
    pipeline, Phase 3 = system-message assembly + caching, Phase 4 = invalidation +
    scheduling, Phase 5 = eval/observability.
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

## 4. Why this is the token-efficient choice

- **Per-conversation:** static 200-token prompt + N tool-call round-trips (each a
  full model invocation over growing context) → bounded card in a **cached**
  prefix + near-zero tool calls for baseline context.
- **Cost is moved off the hot path:** narrative summarization runs offline on a
  cheaper model on a schedule, amortized across many conversations, instead of
  the flagship model re-deriving it live every session.
- **Numbers stay correct for free:** deterministic aggregation means the
  expensive model never has to (re)compute metrics, only reason over them.
