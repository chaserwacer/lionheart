---
name: engineering-standards
description: House engineering standards for C#/.NET, plus a design-before-code survey required for any nontrivial change — new features, refactors, pipeline or orchestration work, data and state modeling, or extending existing behavior. Governs domain-state modeling, code reuse, encapsulation, file organization, functional Result/monad conformance, railway-oriented programming and function composition as the shape of every orchestrator and pipeline, async and concurrency choices, comments, and simplicity. Use before writing or modifying any C# or .NET production code, even when the user never says "standards" or "design" — it is meant to be consulted before implementation starts, not only at review time. Load ef-core-persistence alongside it when the change touches a database, and test-design when it needs tests.
---

# Engineering Standards

Every task is a **production-grade fix or enhancement** by default — never a quick patch or proof of
concept — unless the user says otherwise. Code should be readable, intelligently designed, and
bulletproof while staying as concise as possible. Concise and bulletproof, not clever and defensive.

Deeper material lives one level down; read it when the work touches it:

| Topic | File |
|---|---|
| C# idioms — ADTs, Result composition, totality, modern syntax | `references/csharp-functional.md` |
| Orchestrators — choosing the combinator per step, staying on the track, loops as folds, composition | `references/railway-orchestration.md` |
| Command handlers — guard, stage, commit for fallible async steps that end in a write | `references/command-pipeline.md` |
| When a comment earns its keep | `references/comments.md` |

## Before writing code — the design survey

Work through these before implementing anything nontrivial; the order carries no meaning. Most
avoidable design failures happen because code was written from the current file's point of view
instead of the domain's and the codebase's. This survey is cheap; rework isn't.

### Establish where the state lives
Which store, which schema, which existing model owns this data? Repos routinely have more than one
persistence story with incompatible rules, and picking the wrong one is not recoverable by a later
refactor. If persistence is involved at all, the **ef-core-persistence** skill governs how the model
reaches the database.

### Find the sibling — for conventions, not for standards
Locate the closest analogous implementation already in the repo and take from it the things that are
**arbitrary but must be consistent**: naming, folder layout, how a multi-file feature is packaged,
what this project calls things. Don't scatter loose files among unrelated code.

**Take its packaging, not its design.** A sibling proves where code goes, not that it is good. Judge it
against this document: if it predates a rule here, violates one, or would simply be written better
today, write the better version and say in your summary what you changed and why. Copying a weak
pattern because it is already there is how a codebase's worst decision becomes its convention, and it
is the most common way a standards document gets quietly overridden by the code it was meant to fix.
No count of existing sites using a pattern this document prohibits outweighs the rule; a common
defect is still a defect.

The two halves are separable — match the packaging, improve the code. If no sibling exists, that is
not a reason to hesitate; design it from the standards.

### Enumerate domain states as domain facts
List every state the data can occupy **according to the domain**, not according to what the current
code does with them. Two states the code happens to handle identically today are still two states —
never merge, rename, or collapse domain-distinct states because their handlers currently coincide.
Handling converges and diverges over time; the type system is where the domain's truth lives.

Model closed state sets as an abstract record with sealed concrete subtypes, with factories that
enforce invariants so **invalid states cannot be constructed**. If you believe two states are
genuinely one, ask the user — that is domain knowledge, not a refactor.

### Do a reuse pass
Before writing any logic, answer three questions:
- **Does this logic already exist?** Search the repo first. Re-implementing behavior that already
  lives in a nearby method is a defect, not a style issue. Call the existing implementation, or
  extract it into a shared method both call sites use.
- **Will this logic have a second call site?** If you're adding a second consumer of existing
  behavior, that's the extraction moment — extract at the second call site, every time.
- **Is this composition a known shape?** When orchestrating effects, pipelines, or parallel work, use
  the codebase's existing monadic combinators. If the shape you need has no combinator yet, write it
  as a **reusable, well-named extension** in the monad layer — never as inline plumbing in the
  orchestrator.

### Name new data clusters
When new fields arrive together and describe one concept, they are a new record/value object — do not
flatten them onto an existing client, parent, or DTO object. The test: *does this cluster of data
have a name in the domain?* If yes, it's a type. Encapsulate it, give it its emptiness/presence
semantics, and hang behavior on it where appropriate.

### One job per unit
A method that validates + mutates + notifies is three methods. The same holds a level up: a class or
static class is a module with one job you can name. A command or domain type holds its data and
rules; the command-line parsing, report rendering, and comparisons built around it are modules of
their own. A change that adds to a file already doing several jobs splits it into
smaller modules along the lines the repo already uses elsewhere (not into new interfaces), or says in
its summary why not. A file past about 400 lines is a prompt to count its jobs, not a limit. For
nontrivial behavior, plan the decomposition *now* — name the steps and the orchestrator that chains
them — rather than writing one big method and splitting later.

### Lay out the railway
Where those steps can fail, write the railway down before the code: each step, the combinator its
kind calls for (the table in `references/railway-orchestration.md`), the failure vocabulary each
layer speaks and where `MapError` restates it, and where the chain ends in a `Match`. A step no
combinator fits goes back to the reuse pass above, before the orchestrator is written.

## Compose, don't complicate

Complex systems are complex **orchestrations of small methods**, never complex methods.

- **The railway is the default shape.** Any orchestrator of fallible steps is one chain through the
  monad layer — `Bind`, `Map`, `MapError` and the rest of the vocabulary — from its first step until
  the edge ends it with `Match`. Nothing else inspects which case a result is. Once any step is
  async, the chain stays on `Task<Result<…>>` through the `…Async` combinators, and the chain itself
  is awaited only at the edge, never between its steps. Awaiting a task that is not a `Result`, or an
  `await` inside the monad layer or the command seam, is not what this forbids;
  `references/railway-orchestration.md` has the rule, the shapes, and the short list of exceptions.
- **Orchestrators read as narrative.** A top-level method reads top-to-bottom as the story of what
  happens — a sequence of well-named calls at one level of abstraction. Step-level detail lives one
  level down. A method that both sequences steps and performs them is mixing levels; push the detail
  down.
- **Chain transformations on values.** Build from small functions whose signatures chain — LINQ
  chains, record `with`-expressions, monadic binds, steps that each take a value and return the next
  — never long procedural blocks that mutate locals across dozens of lines. A loop that carries state
  from one iteration to the next is a fold. Each link does one transformation; the chain is the
  algorithm.
- **The extraction criterion is a name, not a line count.** A method earns existence by naming a
  meaningful domain step (`ResolveCarrier`, `ApplyPricingRules`), not by shortening its parent.
  Twenty vague two-line methods (`DoStep1`, `HandleData`) are worse than one clear method — that's
  fragmentation, not composition. If you can't name the step in domain terms, it isn't a step yet.

## Core patterns

These name the rules. The shapes that implement them — the `abstract record`/`sealed` ADT, the
`Bind`/`Map`/`MapError` chain, the async combinator matrix, `Traverse` — are in
`references/csharp-functional.md`, and getting them right is most of the value here. Read it before
writing the first one, not after review.

- **Immutable records, always — for values.** `record` and `readonly record struct` are the default
  for domain data; a mutable class with public setters needs a stated reason, and "the framework
  wants one" is not one. State changes produce a new value through a `with`-expression or a factory,
  never a setter. **An entity is the exception**: it has identity rather than value semantics, so an
  aggregate root is a `sealed class` with private setters. Give a mutable entity a `record` and two
  freshly-created ones compare equal while its hash changes as it mutates — which breaks set and
  dictionary membership, and change tracking with them.
- **Closed state sets are ADTs.** `abstract record` with `sealed` cases and a `private protected`
  constructor, matched by switch expression. Never a `string` status, an `enum` plus a bag of
  nullable fields, or a `bool` pair encoding three states.
- **Functional result pattern.** Expected failures return the codebase's `Result`/monad type carrying
  *why*, never thrown exceptions or bare null/bool returns that hide the reason.
- **Null avoidance.** Model presence/absence explicitly: encapsulated fill-state (an "is empty" check
  on the type), `Option`-style modeling, or record invariants — not scattered null checks.
- **Monadic composition.** Chain and combine via the monad layer; grow that layer with reusable
  combinators rather than working around it.
- **Totality at boundaries.** A function that cannot fail is better than one that returns a `Result`
  nobody checks. Where input is trusted (already-validated storage, an internal caller), prefer a
  total function that always produces a valid value over a partial one that forces a check.

## Simplicity — and what it does not mean

Remove machinery the problem doesn't need: extra layers, one-caller configuration, speculative
extensibility, "just in case" parameters. But "is this needed?" has a different horizon depending on
what you're judging:

- **Types** — judge against the domain's possible states. Domain distinctions are kept even when
  currently handled uniformly. Collapsing them is not simplification; it's deleting domain knowledge.
- **Seams** — the places a boundary will have to move: a store, a provider, a protocol, a schema.
  Keep the seam when the change is **known to be coming** — a decision already made, a migration
  already scheduled, a second consumer already specified. Drop it when the change is merely
  imaginable. "We might need this eventually" is imaginable; "the collection feature moves to
  Postgres next sprint" is known.
- **Machinery** — knobs, layers, generality, indirection. Judge against call sites, but count the
  ones you are **writing**, not only the ones that already exist. Delete configuration nobody sets,
  parameters nobody varies, and indirection added "in case". Keep — and finish — a well-designed
  component whose callers are part of this change or its obvious next step. Some good code must exist
  before anything can call it; the obligation is to write the callers, never to leave the component
  unbuilt.

The failure this guards against runs both ways. Building for an imagined future is waste; refusing to
acknowledge a future you have already committed to is a rewrite you scheduled for yourself. And a
third failure hides behind the first: declining to write the right abstraction because nothing calls
it yet, when what the code actually needed was that abstraction plus the two call sites that use it.

## Names and folders

A reader walking the tree — every folder on the path, then the file name — should have a confident
guess at what the file holds before opening it. That is the test for a name and for a location, and
the two are judged together: the folders carry the context, the file name carries the rest. Naming
is not finished until the path reads.

- **Build names from the domain's own words, and use as many as the description needs.** A longer
  name that says what the thing is beats a short one that has to be looked up; there is no word
  limit. What is not allowed is a rare or novel word standing in for a plain description — the
  reflex to reach for a word that sounds precise ("corpus" for the whole catalog, "mirror" for a
  copy plus everything derived from it) is how a codebase's vocabulary stops being learnable. If
  the repo keeps a glossary, names come from it and a new noun gets its line there in the same
  change.
- **Folders are earned by groups, not by layer alone.** When a project root or a folder has become a
  list, group what belongs together: files about one source, one lifecycle stage (loading, reading),
  one feature. Two similar files rarely justify a folder; three can; five that share a purpose is a
  clear sign. Never a folder for one file, or for a distinction nobody navigates by. Layer folders
  (`Model/`, `Rows/`, `Configurations/`) are fine where the layer is what a reader looks for, and
  sit beneath the purpose folder once a purpose has grown its own of each.
- **Reorganising is part of the change, not a separate cleanup.** A change that adds the fourth or
  fifth file to a folder that has outgrown its shape proposes the new shape — a short tree, in the
  summary, before moving — rather than adding to the pile or leaving it to "a later moves-only PR"
  the user hasn't agreed to. Moves are `git mv` in their own commit, namespace edits in the next, so
  history follows the file.
- **The namespace follows the folder.** A file's namespace is the project's root namespace plus its
  folder path. A neighbour whose namespace disagrees with its folder is not the house pattern to
  match; a new or moved file takes the namespace its folder implies.
- **A rename is a prose change too.** Swapping the word in a doc or a comment can leave a sentence
  that is now false. Read the sentence, not the token.

## Comments

Comments are a liability that must earn their keep — the highest-value one records a *why* that is
invisible in the code, and the worst restates it. The bar, the never-write list, and the
four-question test are in `references/comments.md`.

## After writing

Behaviour this change adds or alters needs a test. The **test-design** skill, where it is installed,
governs what earns one and what shape it takes.
