# C# — functional idioms

The goal is elegance the *reader* experiences, not cleverness the author enjoys. A three-line LINQ
chain beats a fluent DSL nobody else in the repo uses.

Verified against C# 14 / .NET 10 docs, September 2026. Anything not yet GA lives in
[Horizon](#horizon-not-yet-ga-re-verify-before-relying-on-it) at the end, not in the rules.

- [Closed hierarchies as ADTs](#closed-hierarchies-as-adts)
- [Result composition](#result-composition)
- [The async surface](#the-async-surface)
- [Concurrency and streaming](#concurrency-and-streaming)
- [Collections of results](#collections-of-results)
- [Designing the problem type](#designing-the-problem-type)
- [When a combinator earns its place](#when-a-combinator-earns-its-place)
- [Total and partial functions](#total-and-partial-functions)
- [Presence without null](#presence-without-null)
- [Operators on domain types](#operators-on-domain-types)
- [Strategy without a switch](#strategy-without-a-switch)
- [Modern surface worth using](#modern-surface-worth-using)
- [Project defaults](#project-defaults)
- [Horizon (not yet GA)](#horizon-not-yet-ga-re-verify-before-relying-on-it)

## Closed hierarchies as ADTs

A closed set of domain states is an `abstract record` with `sealed` cases:

```csharp
public abstract record Holding
{
    private protected Holding() { }              // seals the hierarchy to this file

    public sealed record Unheld : Holding;
    public sealed record Held(int Copies) : Holding;

    public static Holding Of(int copies) => copies is 0 ? new Unheld() : new Held(copies);
}
```

`private protected` on the base constructor is what makes the set genuinely closed — no case can be
added from another assembly. A static factory on the base is where the mapping from raw data to case
belongs, so callers never construct the wrong one.

Match with switch expressions:

```csharp
var label = holding switch
{
    Holding.Unheld     => "not in collection",
    Holding.Held h     => $"{h.Copies} copies",
    _ => throw new UnreachableException(),
};
```

**C# 14 has no exhaustiveness checking for this**, so the discard arm is load-bearing: throw
`UnreachableException`, never return a default. A default silently absorbs the case somebody adds
next year; the throw turns it into a test failure at the first execution.

The same trap wears a second costume, and it is the one that actually gets written: a catch-all arm
returning a **real answer** — `var other => Refuse(other)` in a state transition — absorbs a newly
added case exactly the way `default` does, while reading as deliberate handling. Enumerate the cases
the method genuinely handles, and leave the discard for the genuinely impossible.

This is the one rule here with a known expiry: the discard arm is the rule today, and the
closed-hierarchy feature that retires it is in the Horizon section.

When a state is *orthogonal* to another rather than alternative to it, model it by **wrapping**, not
by multiplying cases:

```csharp
public sealed record Withdrawn(Holding Underlying, DateTime At) : Holding;
```

Two independent facts across three cases beats one flat set of six, and `w.Underlying is Held` still
answers the original question with the descriptor intact. Note that this shape does not map to a
single relational discriminator — see the **ef-core-persistence** skill.

## Result composition

`Result<T, TProblem>` is the codebase's failure channel. Three combinators are its core; the rest of
the vocabulary is under [When a combinator earns its place](#when-a-combinator-earns-its-place), and
`references/railway-orchestration.md` maps each kind of step to one.

- **`Map`** — a step that cannot fail. Transforms the value, leaves the problem alone.
- **`Bind`** — a step that can fail. Chains fallible operations without unwrapping between them.
- **`MapError`** — a layer boundary. Restates a lower layer's problem in this layer's vocabulary.

```csharp
public static Result<Shipment, Problem> Ship(Order order, Address to) =>
    order.ReadyToShip()                                    // Result<Order, Problem>
        .Bind(ready => Carrier.Quote(ready, to))           // uses the step before: sequential
        .Map(quote => new Shipment(order.Id, quote));
```

`MapError` is what lets a shared library state its own failures and every consumer re-map them,
rather than the library importing a caller's error model. That direction of dependency is the point.

**Type the lambda when the new problem is a case.** `MapError(p => new Failure.Refused(p))` infers
the case type, not the base, so the chain produces `Result<T, Failure.Refused>` and stops compiling at
the next step that expects `Result<T, Failure>`; and a nested record's constructor is not a method
group, so `MapError(Failure.Refused)` does not compile either. Give the lambda its return type —
`MapError(Failure (DomainProblem p) => new Failure.Refused(p))` — or give the base type static
factories that return the base.

Validation functions are **standalone, pure, and static**, returning `Result` and designed for
composition — not private helpers buried in the type that needs them. That is what makes them
testable and reusable across the wire boundary and the storage boundary alike.

Independent validators are not a `Bind` chain, because neither needs the other's value. At the wire
they accumulate with `ZipAll` (see [Collections of results](#collections-of-results)), so the caller
hears every problem at once:

```csharp
public static Result<Client, Problem> Create(string name, string email) =>
    ClientName.Of(name)
        .ZipAll(EmailAddress.Of(email), (n, e) => new Client(n, e));   // both run, both reported
```

`using static Result<Client, Problem>` in files that construct many `Success(...)` / `Failure(...)`
values keeps the noise down.

Switch on domain values, never on success or failure — see "Stay on the track" in
`references/railway-orchestration.md`.

## The async surface

Real orchestrations are `Task<Result<T, TProblem>>` — **two monads stacked**. C# has no
higher-kinded types, so no single generic transformer can unwrap both; every combinator is
hand-written for this exact stack, and each one has four cells:

| | selector is sync | selector is async |
|---|---|---|
| **source `Result<T,E>`** | `Bind`, `Map`, `MapError`, `Match` | `BindAsync`, `MapAsync`, `MatchAsync` |
| **source `Task<Result<T,E>>`** | `BindAsync`, `MapAsync`, `MapErrorAsync`, `MatchAsync` | `BindAsync`, `MapAsync`, `MatchAsync` |

Every combinator is named by the same rule: no suffix when neither the source nor the selector
involves a `Task`, `Async` when either does. `Tap`, `Ensure` and the rest follow it too, so
`TapAsync` is the form on a `Task` source whether its action is synchronous or returns a `Task`.

**Fill the whole matrix, or the chain breaks at the empty cell** and the caller has to `await` back
into the shape the other overload wanted. `Match` is the one most often forgotten and the most
expensive to omit, because it terminates chains — a missing `MatchAsync` forces an `await` at the end
of every pipeline that has one.

**Two cells need `[OverloadResolutionPriority(1)]` and misbehave silently without it** — the
`Task`-source `MapAsync` and `MatchAsync` with an async selector. A lambda returning `Task<U>` also
satisfies `Func<T, U>` with `U = Task<U>`, so each loses overload resolution to its synchronous
sibling of the same name: `MapAsync` yields `Result<Task<U>, TProblem>`, and `MatchAsync` a
`Task<Task<U>>`. Both compile, run, and hand the caller an unawaited task. Mark those cells when you
write them. (The `Result`-source async cells have no same-named synchronous sibling — that is `Map`
and `Match` — so they are not exposed.)

Overload resolution then picks the right cell and a chain of async steps reads as one expression:

```csharp
public Task<Result<CopyView, Failure>> AddCopyAsync(
    CollectorId collector, IssueId issue, CopyDraft draft, CancellationToken ct) =>
    catalog.ExistsAsync(issue, ct)                     // Task<Result<…>> — chain starts here
        .BindAsync(_ => LoadEntryAsync(collector, issue, ct))
        .MapAsync(entry => CopyView.Of(entry));
```

**`await (await …)` is the diagnostic.** Writing it means the chain fell onto the `Result`-source
overload and had to be awaited into position first. The fix is never another `await` — it is calling
the `Task`-source overload, or adding it if it is genuinely missing. Two nested awaits in one
expression is a missing overload every time.

Return `Task<Result<…>>` rather than `async Task<Result<…>>` from a method that only forwards a
chain. No state machine is generated, and the method reads as the composition it is.

**Cancellation is a parameter, never a capture.** Every async step takes `CancellationToken ct`
explicitly. A combinator that runs work concurrently must hand its delegates a token *derived* from
the caller's — a delegate that closes over the ambient token cannot be cut short by the combinator
that owns its lifetime.

## Concurrency and streaming

"Async all the way" is the floor, not the standard. Match the shape to the API the runtime already
ships rather than hand-rolling it:

| Shape | Use | Not |
|---|---|---|
| N independent operations, need all results | `Task.WhenAll` | a loop of awaits |
| N operations, bounded concurrency | `Parallel.ForEachAsync(src, new ParallelOptions { MaxDegreeOfParallelism = n, CancellationToken = ct }, body)` | `SemaphoreSlim` + `WhenAll` |
| React as each finishes — short-circuit, stream partials | `await foreach (var t in Task.WhenEach(tasks))` | a `WhenAny`-and-remove loop |
| A sequence too large to materialize | `IAsyncEnumerable<T>` + `await foreach` | `ToListAsync()` |
| Producer/consumer with backpressure | `System.Threading.Channels` | a `BlockingCollection` |
| A timeout on an operation that takes no token | `task.WaitAsync(timeout, ct)` | `WhenAny` with `Task.Delay` |

`Parallel.ForEachAsync` defaults to `Environment.ProcessorCount` — which is a **CPU** bound applied to
**IO** work. Set `MaxDegreeOfParallelism` deliberately from the downstream limit (connection pool
size, API rate limit), and say which in a comment. Its `body` is `Func<TSource, CancellationToken,
ValueTask>`, so the token arrives as a parameter rather than a capture.

`Task.WhenEach` yields each task once it has completed, as an `IAsyncEnumerable` — the docs do not
define the exact order, so use it to react early, never to infer ranking. That is what makes a
fail-fast fan-out expressible without a hand-rolled trip wire: enumerate, and `break` on the first
failure. That loop inspects each result's case, so it is written once, inside a fan-out combinator in
the monad layer (a `WhenAllOrFirstError`), never in an orchestrator. Prefer it to bespoke
cancellation plumbing — concurrency primitives written by hand are the most expensive code in any
codebase to keep correct.

**.NET 10 ships `System.Linq.AsyncEnumerable` in the BCL**, so `Where`, `Select`, `Take`,
`FirstOrDefaultAsync` and the rest compose over `IAsyncEnumerable<T>` with no package at all. Delete
any `System.Linq.Async` reference — it conflicts with the built-in one. This is what makes streaming
a *default* rather than a special case: a streamed sequence is now as composable as a `List<T>`, and
there is no longer a reason to materialize one just to keep using LINQ on it.

Mark the token parameter of an `IAsyncEnumerable` method `[EnumeratorCancellation]` or the
`await foreach`'s token never reaches it.

On streaming from the database: EF's `AsAsyncEnumerable()` streams rows instead of buffering them.
Materializing a list is right for a page; it is wrong for a report, an export, or anything feeding a
`COPY`. Two caveats decide whether it actually streams — a retrying execution strategy and
`AsSplitQuery` both force EF to buffer internally — and for anything that does real work per row a
resumable keyset page loop beats holding a reader open. See
the `ef-core-persistence` skill's `references/querying.md`.

**`ConfigureAwait(false)` — decide once, on evidence.** Modern hosts (ASP.NET Core, Azure Functions
isolated worker, console) install no `SynchronizationContext`, so it prevents no deadlock there and
is noise. It still matters in a library that could be consumed by a host that has one (WinForms, WPF,
legacy ASP.NET). Applying it uniformly "to be safe" is cargo cult; omitting it from a genuinely
reusable library is a real bug. State which situation the assembly is in, once, and be consistent.

## Collections of results

The combinator everyone needs and nobody writes first: `N` results into one result of `N`.

```csharp
public static Result<IReadOnlyList<U>, TProblem> Traverse<T, U, TProblem>(
    this IEnumerable<T> source, Func<T, Result<U, TProblem>> f);          // fail-fast

public static Result<IReadOnlyList<U>, TProblem> TraverseAll<T, U, TProblem>(
    this IEnumerable<T> source, Func<T, Result<U, TProblem>> f)           // accumulating
    where TProblem : ICombinableProblem<TProblem>;
```

For a fixed handful of inputs of different types the pair is `Zip` (fail-fast) and `ZipAll`
(accumulating). The recommended form takes the function that combines the values, so nothing is built
only to be taken apart; a `Zip` that returns a tuple is acceptable when the next step deconstructs it
at once. `ZipAll` needs the semigroup below — add it when wire validation needs it.

The choice between them is a boundary decision, not a taste:

- **Fail-fast (`Bind`, `Traverse`, `Zip`) inside the application.** A pipeline step that fails makes the
  remaining steps meaningless, and the first problem is the whole story.
- **Accumulate (`TraverseAll`, `ZipAll`) at the wire.** Someone filling in a form must be told every field that
  is wrong, once. A `Bind` chain over five validators reports one error and hides four, then does it
  again on resubmit. This is the most common place the monad is used against its own grain.

Accumulation requires the problem type to be a **semigroup** — a `Combine` that is associative, so
any number of problems folds into one regardless of grouping. Declare that as an interface constraint
so a problem type that cannot combine simply will not compile into an accumulating combinator, rather
than silently discarding failures.

## Designing the problem type

The failure channel deserves the same treatment as domain state, and usually does not get it.

- **A closed ADT, not a string, a `bool`, or an exception.** `abstract record Failure(string Code)`
  with `sealed record` cases. The `Code` is the stable wire identifier; the case is what the code
  matches on.
- **One per layer, restated at the boundary.** A library states its own refusals and never imports
  its caller's error model; `MapError` at the seam translates once. That direction of dependency is
  the entire point of having `MapError` at all.
- **Cases carry only what the caller can act on.** A refusal that needs a paragraph to explain is
  two cases.
- **Add `Combine` only when a combinator can observe two failures at once.** Until an accumulating
  combinator exists, the semigroup is unused ceremony.

Map failures to transport concerns (HTTP status, exit code, retryability) **once, at the edge**.
A domain refusal that knows what a 404 is has already leaked.

## When a combinator earns its place

The monad layer is a vocabulary. Two things can go wrong with it, and only one of them is
over-building — the more common failure is building the *wrong* words.

- **The criterion is a recurring *shape*, not a step.** `OrElseAsync` (fallback), `TapError` (observe
  without altering), `Traverse` (n-to-one) are shapes. "Load the user then check the tenant" is a step
  and belongs in a named method.
- **Build the everyday shapes before the exotic one.** The classic failure is an intricate concurrency
  primitive landing while `Traverse` and `MatchAsync` are still missing, so ordinary code hand-rolls
  what the layer should have supplied. Fill the matrix in
  [The async surface](#the-async-surface) first; it is where most of the value is.
- **Ship it wired up.** A new combinator arrives with its call sites converted in the same change —
  sites that already exist, or the ones you are writing alongside it. That is what separates a
  vocabulary word from a museum piece. It is a reason to finish the work, never a reason to leave the
  word unwritten.
- **Name it for the shape, not the use case.** `WhenAllOrFirstError` describes a shape;
  `LoadUserAndTenantAsync` is a method wearing a combinator's clothes.
- **Prefer the standard names.** `Bind`, `Map`, `Traverse`, `OrElse`, `Tap` carry meaning a reader
  already has. A bespoke name for a standard shape costs every future reader a lookup.

The everyday shapes beyond the core, which orchestrators hand-roll when they are missing:

**`Ensure` — a rule that refuses.** A predicate and the problem to report when it fails; the value
passes through unchanged when it holds. It replaces a `Bind` whose lambda is an `if` returning either
the same value or a failure.

```csharp
public Result<T, TProblem> Ensure(Func<T, bool> holds, Func<T, TProblem> problem);
```

**`Tap` and `TapError` — observe without altering.** An action run on the value (`Tap`) or the
problem (`TapError`), for a log line, a metric, a notification. Each runs only on its own track and
returns what it was given.

**`Catch` — a throwing call at an I/O boundary.** Runs the call and converts the **named** exception
types into domain problems; everything else propagates, the same rule as the command seam's `catch`
filter.

```csharp
public static Task<Result<T, TProblem>> CatchAsync<T, TException, TProblem>(
    Func<CancellationToken, Task<T>> call, Func<TException, TProblem> problem, CancellationToken ct)
    where TException : Exception;
```

A catch-all is legitimate in one place only: a boundary whose job is to *record* that a step failed —
marking a run or a ledger row failed before the exception goes on. That boundary excludes
cancellation (`catch (Exception e) when (e is not OperationCanceledException)`), and it never turns
the exception into a domain refusal, because that is how a bug starts looking like a business answer.

**Folds and unfolds — a loop that carries state.** A **fold** walks a sequence and carries one
accumulated value from each element to the next; LINQ's `Aggregate` is one. The **fail-fast** fold's
step returns a `Result`, and the fold stops at the first failure. An **unfold** runs the other way: the
sequence is not known up front, and each step produces the input to the next — the next page of a
listing comes from the last one. Its step answers with a small closed type, keep going with a new
state or stop with a result:

```csharp
public abstract record Loop<TState, TResult>
{
    private protected Loop() { }

    public sealed record Continue(TState State) : Loop<TState, TResult>;
    public sealed record Done(TResult Result) : Loop<TState, TResult>;
}

public static Task<Result<TState, TProblem>> FoldAsync<T, TState, TProblem>(
    IEnumerable<T> source, TState seed,
    Func<TState, T, CancellationToken, Task<Result<TState, TProblem>>> step, CancellationToken ct);

public static Task<Result<TState, TProblem>> FoldAsync<T, TState, TProblem>(
    IAsyncEnumerable<T> source, TState seed,
    Func<TState, T, CancellationToken, Task<Result<TState, TProblem>>> step, CancellationToken ct);

public static Task<Result<TResult, TProblem>> UnfoldAsync<TState, TResult, TProblem>(
    TState seed,
    Func<TState, CancellationToken, Task<Result<Loop<TState, TResult>, TProblem>>> step,
    CancellationToken ct);
```

An EF query implements both `IEnumerable<T>` and `IAsyncEnumerable<T>`, so passing one straight to
`FoldAsync` is an ambiguous call. Pass `query.AsAsyncEnumerable()`, which is the overload you want
anyway: it streams instead of blocking on a synchronous enumeration.

The step runs first on the seed, and the cursor lives in the state, so a page number works as well as a link, and the result can differ
from the state — the walk's outcome rather than its bookkeeping. The `IAsyncEnumerable` overload is
for a streamed source, which is the case that most needs a fold, since it must never be materialized
to use one. Each body is the `while` or `await foreach` loop, and the case test, that orchestrators no
longer write — this is the place they belong. The methods sit on a non-generic `static class Result`,
which can sit beside `Result<T, TProblem>` because the two differ in arity. Where each item is handled
independently, with nothing carried between them, the shape is `Traverse`, not a fold.

How an orchestrator uses these, including flattening a nested result and when not to, is
`references/railway-orchestration.md`.

## Total and partial functions

Two entry points per value object, and conflating them causes outages:

```csharp
public static Result<Grade, Problem> Of(decimal value);   // partial — validates wire input
internal static Grade FromStorage(decimal value);         // total — never throws
```

The partial one guards the boundary. The total one is for data that has already passed the boundary
— storage, an internal caller — and it must produce *some* valid value for any input the type can
hold, degrading rather than throwing. The reason is blunt: one malformed row must not make an entire
collection unloadable.

Where a total function has a degenerate branch, make the branch unreachable with a database check
constraint rather than leaving it merely lenient — otherwise a data defect reads as a valid-but-wrong
state, which is worse than an error.

## Presence without null

Model absence as a case, not as `null`:

```csharp
public abstract record GradeEstimate
{
    public sealed record Ungraded : GradeEstimate;
    public sealed record Estimated(Grade Value) : GradeEstimate;
}
```

Don't add a `Maybe<T>` on top of this. Two absence vocabularies in one codebase is worse than
either alone, and the domain record is the one that carries a name.

The test for whether a boolean column is warranted: if the fact is *derivable* from another
relationship's fill state, derive it. An `IsOwned` flag beside a copies collection is a second source
of truth that will eventually disagree with the first.

## Operators on domain types

Comparison operators on a value object are worth defining — they are what let a domain type read like
a number at the use site. Two obligations come with them.

**An operator must be total, so check that the relation actually is.** Where two values can be
incomparable, the operator has no failure channel and must invent an answer — a **partial order
wearing a total order's syntax**. The familiar algebra then fails: `a < b || a >= b` is not a
tautology, so two complementary predicates do not partition the set. Document it on the type; it is
invisible in the signature. Where the incomparability is itself information, keep a
`Result`-returning method beside the operator and use that in domain logic.

**In a LINQ to Entities query the operator body is never executed.** EF reads the expression tree's
node kind and ignores the operator method, so any guard inside the operator is absent from the
generated SQL — and the comparison only translates while both operands reduce to a single column.
Put the comparison on the type rather than on its wrapped value, though: `id.Value > n` is a member
access EF cannot see through at all. See the **ef-core-persistence** skill.

## Strategy without a switch

An **open** set of pluggable behaviours is an abstract record with concrete record implementations,
registered in DI and injected as `IEnumerable<T>` — adding one is adding a file and a registration,
never editing a switch.

The decision that matters is which set you have. **Closed and known → ADT plus a switch expression**,
which is simpler and gives you the compile-time story. Reaching for DI dispatch on a closed set trades
exhaustiveness for a registration you can forget.

## Modern surface worth using

Calibrated to C# 14 / .NET 10, as of the verification date at the top of this file. Each entry
below earns its place by serving something this skill already asks for.

### Language — C# 14 (.NET 10)

- **Extension members.** An `extension` block declares extension *properties* and *static* extension
  members, not just methods — and groups them by receiver, which is what a vocabulary layer wants:

  ```csharp
  public static class ResultExtensions
  {
      extension<T, E>(Task<Result<T, E>> source)          // instance-like members
      {
          public Task<Result<U, E>> BindAsync<U>(Func<T, Task<Result<U, E>>> f) => …;
          public Task<bool> IsFailure => …;               // extension property
      }
  }
  ```

  This is the right shape for both vocabularies this skill mandates — the monad layer's combinators
  and `IQueryable<T>` named queries. Grouping by receiver is the point; a flat static class of
  unrelated `this`-parameter methods is what it replaces.

  The limit that decides which methods can move: **every type parameter the receiver needs is declared
  on the `extension` block**, so a signature whose method-level type parameter must precede the
  receiver's cannot be ported and stays a `this`-parameter method. Inference is weaker than classic
  extension methods in mixed-generic cases. Both forms lower identically, so a partial migration is
  binary-compatible and mixing the two is fine.
- **`field` keyword** — a validated property with no hand-written backing field:

  ```csharp
  public string Message { get; set => field = value ?? throw new ArgumentNullException(nameof(value)); }
  ```

  Note the trap: `field` shadows any member actually named `field` inside an accessor. Use `@field`
  to disambiguate, or rename.
- **User-defined compound assignment is the one C# 14 feature to refuse.** It declares a
  `void`-returning *instance* operator that mutates the target in place, added so `BigInteger`-scale
  values can skip an allocation per `+=`. On a domain value object it silently turns `total += line`
  from "produce a new value" into "mutate the one you had" — the exact semantics every other rule
  here exists to preserve. Numeric and buffer types only.

### Language — C# 13 (.NET 9)

- **`System.Threading.Lock`** over `lock(object)`. The `lock` statement recognizes the type and emits
  the newer API instead of `Monitor`; changing the field's type is the whole migration. The trap: the
  expression's static type must be **precisely** `System.Threading.Lock` — assign it to an `object`
  or pass it through a generic `T` and you silently get `Monitor` back.
- **`OverloadResolutionPriorityAttribute`** — for adding a better overload to a library without
  breaking or ambiguating callers. This is the tool for filling gaps in a combinator matrix, and
  Microsoft's own guidance is that it is for library authors and should be used sparingly.

### Runtime

- **`readonly record struct`** for small value objects on hot paths — no allocation per row, and it
  sidesteps the mutable-type comparer problem in EF value converters.

- **`TimeProvider`** over `DateTime.UtcNow` *in domain logic*, so time-dependent behaviour is
  testable. Storage-side defaults (`now()` in SQL) are a different concern and fine as they are.
- **`Guid.CreateVersion7()`** (.NET 9, RFC 9562) for any `Guid` that becomes a database key. The
  timestamp leads, so successive values sort together and inserts land in one region of the index
  instead of scattering across it the way v4 does. Take the `DateTimeOffset` overload and feed it
  `TimeProvider` so identity generation stays testable. Be precise about what is actually promised:
  Microsoft documents only "uses `UtcNow`" and "seeds the random sub-fields with random data" — the
  ordering property follows from the RFC layout and the runtime implementation, not from a documented
  guarantee. Within one millisecond the low bits are random, so it is **not** monotonic. Never use it
  as a sequence or a cursor.
- **`[JsonDerivedType]` on every case** is the standing cost of serializing an ADT with
  System.Text.Json. It is the cost of the shape, never a reason to flatten it; the runtime feature
  that removes it is in the Horizon section.
- **`FrozenSet<T>` / `FrozenDictionary<K,V>`** (.NET 8) for lookup tables built once at startup and
  read forever — an allowed-values list, a code-to-case map. Slower to construct, faster to read than
  the immutable collections. A `static readonly` list you only ever call `Contains` on is the tell.

## Project defaults

`net10.0`, `LangVersion latest` (C# 14), `Nullable` enabled, `ImplicitUsings` enabled. Layering:
entry points → Core (domain + application logic) → Infrastructure (IO, external dependencies), with
Core never depending on Infrastructure and IO kept at the edges. Ports and adapters: interfaces in
Core, implementations in Infrastructure.

## Horizon (not yet GA; re-verify before relying on it)

Everything here was preview at the verification date above, with syntax documented as subject to
change. Nothing in it changes a rule on this page until it ships; when it does, re-verify against the
release notes and then edit the rule.

- **Closed hierarchies (C# 15 / .NET 11).** `closed record Holding` replaces `abstract record
  Holding`: the compiler gets the full case list and an exhaustive `switch` becomes legal with no
  discard arm. Everything the shape depends on survives — value equality, `with`, positional
  deconstruction — so the only edit is deleting the `UnreachableException` arms. Two details for the
  day of the switch: `closed` is implicitly `abstract` and cannot be combined with it, and it is
  **not transitive** — an intermediate level in a three-deep hierarchy needs its own `closed`.
- **`union` (C# 15) is a different tool**, and reaching for it is the trap. Microsoft's tutorial uses
  `union Result<T>(T, Exception)`, which makes it look like the successor to both shapes on this
  page. It is not: a `union` lowers to a plain struct holding an `object?` — no value equality, no
  `with`, no deconstruction, and value-type cases box. It exists to compose *unrelated* types. Domain
  state sets and `Result` both stay records.
- **`JsonSerializerOptions.InferClosedTypePolymorphism` (.NET 11).** System.Text.Json infers
  polymorphic metadata for a `closed` hierarchy instead of requiring `[JsonDerivedType]` on every
  case.
- **Project defaults on `net11.0` / C# 15.** The only change that touches this document is
  `abstract record` → `closed record` for state sets.
