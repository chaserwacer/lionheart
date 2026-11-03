# Railway orchestration

An orchestrator of fallible steps is a **railway**: two tracks, success and failure, and every step
a piece of track that either carries the value on or switches it to the failure line, where it rides
untouched to the end. The name is Scott Wlaschin's ("railway-oriented programming"). In this house it
is not one style among several — it is the shape every orchestrator of fallible steps takes.

`csharp-functional.md` is the reference for what each combinator is and how to build the async
matrix. This file is how to shape an orchestrator out of them, and what to do when one is missing.

- [Choose the combinator by the step](#choose-the-combinator-by-the-step)
- [Stay on the track](#stay-on-the-track)
- [Nested results](#nested-results)
- [Loops are folds](#loops-are-folds)
- [Carrying earlier values forward](#carrying-earlier-values-forward)
- [Composition beyond Result](#composition-beyond-result)
- [Where the railway stops](#where-the-railway-stops)

## Choose the combinator by the step

Name the steps first (the design survey's "Lay out the railway"), then give each one the combinator
its kind calls for. Every row has async cells as well, named by the rule in
[The async surface](csharp-functional.md#the-async-surface): the first step that returns a `Task`
puts the whole chain on `Task<Result<…>>`, and every combinator after it is the `…Async` form. The
chain is awaited once, at the edge — or not at all, when the method returns it.

| The step… | Combinator |
|---|---|
| can fail | `Bind` |
| cannot fail | `Map` |
| is a yes/no rule that refuses with a reason | `Ensure` |
| is a side effect that must not change the value — a log line, a metric, a notification | `Tap` / `TapError` |
| crosses a layer, so the failure vocabulary changes | `MapError` |
| has a fallback when it fails | `OrElse` |
| calls code that throws, at an I/O boundary | `Catch` |
| runs over many items | `Traverse` (fail-fast) / `TraverseAll` (accumulating) |
| needs several independent inputs | `Zip` / `ZipAll` |
| repeats, carrying state from one iteration or page to the next | a fail-fast fold or unfold |
| ends the chain | `Match`, at the edge only |

A step that fits no row is one of two things. A **one-off decision** is a named method that
returns a `Result`, built from the combinators where they can express it — recovering from one
specific failure is an `OrElse` with a switch on the problem's value. Where the decision is
genuinely *about* several results at once (pick the better of two attempts, reconcile a partial
answer with a complete one), it may match on them — the last entry in the closed list
below. A **shape that recurs** across orchestrators is a combinator the layer is missing; the
criterion is in [When a combinator earns its place](csharp-functional.md#when-a-combinator-earns-its-place).
Neither is a reason to leave the track.

## Stay on the track

The rule, stated by what the code does rather than how it is spelled: **no code outside the monad
layer and the edge inspects which case a `Result` is.** Each of these is that, and each is a defect:

- testing the case — `is Success<…>`, `is not Success<…>`, `is Failure<…>`;
- casting to a case — `((Failure<…>)x).Problem`, or reaching for `.Value` on a success just checked;
- a `switch` expression or statement over the cases — `Success<…> s => …`, `Failure<…> f => …` —
  which is the same test with a different spelling, and the likeliest rewrite once the cast is gone;
- a `Match` in the middle of a chain whose arms both build a `Result` again — that is a `Bind`, an
  `OrElse`, or a `MapError` written out by hand;
- a nested `Result<Result<…>>` matched arm by arm (see [Nested results](#nested-results));
- a loop that returns `Failure` early from its body (see [Loops are folds](#loops-are-folds)).

Two more ways off the track inspect nothing yet, but lead straight to the list above. Both are
defects in an orchestrator; neither applies inside the monad layer or the command seam, or to a
task that is not a `Result`:

- **awaiting the chain between steps** — `await` into a local, then continuing with the synchronous
  `Bind`/`Map`. The chain is now two statements, and the second is where the hand check arrives.
  Call the `…Async` form on the task instead; if the cell is missing, add it;
- **blocking on the task** — `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` — to reach a
  synchronous combinator. That trades a missing overload for a blocked thread, and a deadlock
  wherever a synchronization context is installed.

```csharp
// Off the track: the cast re-states what Bind already does, one site at a time.
var page = await FetchPageAsync(url, ct);
if (page is not Success<Page, Problem> read)
    return Result<Tally, Problem>.Failure(((Failure<Page, Problem>)page).Problem);
return Result<Tally, Problem>.Success(tally.Add(read.Value));

// On it.
return FetchPageAsync(url, ct).MapAsync(page => tally.Add(page));
```

Every one of these is a combinator written out longhand, badly: the short-circuit is re-implemented at
each site, the failure's type is spelled out where it could be inferred, and the method stops reading
as the story of the steps.

**Where a case may be inspected** is a closed list:

- **Inside the monad layer**, where the combinators are implemented. A hand-written case test and a
  loop with an early return belong here and nowhere else.
- **The command seam**, whose one `is not Success` check decides whether to save
  ([The command seam](command-pipeline.md#the-command-seam)). It is written once per application.
- **The transport entry point** — an HTTP endpoint, a function trigger, a CLI `Main` — where the
  chain ends. The tool there is `Match` / `MatchAsync`, not a hand check.
- **A named decision over several results**, when the decision needs to see which case each one
  is and no combinator states it. Five conditions keep it from becoming the old escape hatch:
  - it is a small pure method whose whole job is that decision;
  - its name says what it decides;
  - it returns a `Result`, so its callers stay on the track;
  - it matches with a `switch` over the cases, never a cast or a `.Value` reach-in;
  - a one-line comment says why no combinator fits.

  The same decision appearing a second time makes it a recurring shape, and a combinator.
  A single result branched on by hand never qualifies, however awkward the chain feels: that is a
  missing combinator.

A private helper that returns `Task`, `void`, or a tuple is not an edge. It returns the `Result` and
lets its caller chain on, or it takes a value that is already unwrapped. Tests are outside the rule:
asserting which case came back is their job.

**A missing shape is fixed by adding the combinator**, never by another cast: write it in the monad
layer with its law tests (the **test-design** skill's `references/functional-shapes.md`), and convert
the sites that hand-roll it in the same change ([Ship it wired
up](csharp-functional.md#when-a-combinator-earns-its-place)). How many sites already use the old
idiom does not count — see "Find the sibling" in the skill.

**Branch on values, never on success.** Deciding what to do with a domain value — an order that is
open, shipped, or cancelled — is a switch expression, usually inside a `Bind`. Deciding what to do
because the previous step succeeded or failed is what the combinators are for.

## Nested results

`Result<Result<T, TInner>, TOuter>` turns up for two different reasons, and they get opposite fixes.

**Accidental nesting** is a `Map` that should have been a `Bind`: a step returned a result in another
vocabulary and `Map` wrapped it instead of joining it. Restate the inner vocabulary inside a `Bind` and
the nesting never forms:

```csharp
fetched.Bind(body => Parse(body).MapError(Problem (ParseProblem p) => new Problem.Unreadable(p)))
```

**Meaningful nesting** is two domain states. The outer failure means *the run failed*; an outer success
holding an inner failure means *the run finished its work, but the source stopped it* — reported,
logged, and retried differently. Flattening them merges two states the domain distinguishes, which
the skill forbids. Give the inner answer its own name, carry it on the success track, and match it as
a value:

```csharp
public abstract record WalkOutcome
{
    private protected WalkOutcome() { }

    public sealed record Completed(Tally Tally) : WalkOutcome;
    public sealed record StoppedBySource(Tally Tally, SourceRefusal Reason) : WalkOutcome;
}
```

A `Result<WalkOutcome, Problem>` says the same thing as the nested result, in words, and the edge
switches on `WalkOutcome` like any other domain value. A nested result matched arm by arm is never the
answer to either case.

## Loops are folds

A `while` or `foreach` that carries state across iterations and returns `Failure` early is a
fail-fast fold, or an unfold when each iteration produces the input to the next. Both are defined,
with their signatures, in [When a combinator earns its
place](csharp-functional.md#when-a-combinator-earns-its-place); this is how an orchestrator uses them.

A paging crawl is the common case. The loop becomes a state record and one step that turns a state
into either the next state or the final outcome — and here a refusal from the source ends the walk
without failing the run:

```csharp
private sealed record Crawl(Uri Page, Tally Tally)
{
    public Loop<Crawl, WalkOutcome> After(SourcePage page) =>
        page.Next is { } next
            ? new Loop<Crawl, WalkOutcome>.Continue(new Crawl(next, Tally.Add(page)))
            : new Loop<Crawl, WalkOutcome>.Done(new WalkOutcome.Completed(Tally.Add(page)));

    public Loop<Crawl, WalkOutcome> Stop(SourceRefusal reason) =>
        new Loop<Crawl, WalkOutcome>.Done(new WalkOutcome.StoppedBySource(Tally, reason));
}

public Task<Result<WalkOutcome, Problem>> CrawlAsync(Uri first, CancellationToken ct) =>
    Result.UnfoldAsync(new Crawl(first, Tally.Empty), CrawlPageAsync, ct);

private Task<Result<Loop<Crawl, WalkOutcome>, Problem>> CrawlPageAsync(
    Crawl crawl, CancellationToken ct) =>
    FetchPageAsync(crawl.Page, ct)                            // Task<Result<SourceAnswer, Problem>>
        .BindAsync(answer => answer switch
        {
            SourceAnswer.Served served => SavePageAsync(served.Page, ct)   // a failed save fails the run
                .MapAsync(_ => crawl.After(served.Page)),
            SourceAnswer.Refused refused => Task.FromResult(
                Result<Loop<Crawl, WalkOutcome>, Problem>.Success(crawl.Stop(refused.Reason))),
            _ => throw new UnreachableException(),
        });
```

What the loop hid is now named: what is carried between pages (`Crawl`), how the walk ends
(`After`, `Stop`), and what one page does (`CrawlPageAsync`). The switch is on a domain value — what
the source answered — and the failure track is left to the combinators.

## Carrying earlier values forward

A step late in a chain often needs a value from early in it. In order of preference:

1. **A named intermediate record**, when the values together have a name in the domain
   (`PricedOrder(Order, PriceList)`). It is the "name new data clusters" rule applied to a pipeline,
   and a later reader sees the concept rather than a pair.
2. **`Zip`**, when the inputs are independent of each other — neither step needs the other's result.
3. **A tuple**, for two short-lived values that the very next step takes apart. Past two, or past
   one step, the positions stop meaning anything. A `Zip` that returns a tuple falls under this.

Where three or more steps each need values from the ones before, **LINQ query syntax** is allowed. With
`Select` and `SelectMany` defined over `Result` and `Task<Result>`, the compiler turns `from … in`
clauses into `SelectMany` calls — nested binds — with every earlier value in scope:

```csharp
public static Result<Invoice, Problem> Bill(OrderId id, Catalog catalog) =>
    from order   in Orders.Find(id)
    from priced  in catalog.Price(order)
    from invoice in Invoice.Issue(order.Customer, priced)
    select invoice;
```

Written with `Bind`, the same chain nests a lambda per step to keep `order` in scope. The cost is
real, so weigh it: `SelectMany` needs its own overloads for every mix of `Result` and `Task<Result>`
sources and selectors — the matrix again — and some readers find query syntax over something that is
not a collection unfamiliar. `await` is allowed only in the source of the first `from` clause, so
each later step must return its `Task<Result<…>>` rather than await it. Don't add `Where`: a filter
has no problem to report, so it has no honest meaning on a `Result` — that is `Ensure`.

## Composition beyond Result

The railway is one case of a wider habit: build behaviour by composing small functions rather than by
writing large methods or class hierarchies.

- **Steps are small functions whose signatures chain** — the output of one is the input of the next.
  Pure and static wherever the step does no I/O; a step that needs a dependency takes it as a
  parameter or reaches it through the instance, never through hidden state.
- **Higher-order functions take the steps.** When several orchestrators share a shape and differ in
  their steps, write the shape once as a function that takes the steps as delegates.
- **A pipeline can itself be a value.** Where the same shape recurs — several exports, several
  importers — prefer a record made of composed steps, run by one shared function, over an interface
  or an abstract base class:

  ```csharp
  public sealed record Export<TRow>(
      string Name,
      Func<CancellationToken, Task<Result<IReadOnlyList<TRow>, Problem>>> Read,
      Func<IReadOnlyList<TRow>, Result<IReadOnlyList<TRow>, Problem>> Check,
      Func<IReadOnlyList<TRow>, CancellationToken, Task<Result<int, Problem>>> Write);

  public static Task<Result<int, Problem>> RunAsync<TRow>(Export<TRow> export, CancellationToken ct) =>
      export.Read(ct)
          .BindAsync(export.Check)
          .BindAsync(rows => export.Write(rows, ct));
  ```

  Each export is a value built from named steps; the path that runs them is written once. The steps
  are visible, a test swaps one without a mocking framework, and there is no base class whose
  template method every subclass has to understand. An export whose body is one opaque delegate has
  the record but not the benefit — the steps are what should be visible.

C# has no pipe operator, and **none should be built.** A homemade `Compose`, `Pipe`, or an operator
overloaded to look like one is a private dialect every reader has to learn. Named methods chained with
`.Bind` and `.Map` are the composition.

## Where the railway stops

`Result` is for failures the domain can name and a caller can act on: not found, refused, conflicting,
unavailable. Bugs and infrastructure faults — a null where none can be, a lost connection, a schema
mismatch — stay exceptions, and propagate to the one handler at the edge that logs them. Wlaschin makes
this point himself in "Against Railway-Oriented Programming": wrapping every exception in a `Result`
turns the failure type into a catalogue of things nobody can handle, and hides real faults inside
values nobody inspects. Which exceptions `Catch` may convert, and the one place a catch-all is
legitimate, are under its entry in [When a combinator earns its
place](csharp-functional.md#when-a-combinator-earns-its-place).
