# Testing the functional shapes

The forms these tests take. Each one exists because the compiler is not watching the thing it
asserts. Examples use xUnit (`[Fact]`, `[Theory]`, `TheoryData`) and FsCheck (`[Property]`) for
concreteness; the project's existing framework wins.

- [Closed hierarchies](#closed-hierarchies)
- [Result pipelines](#result-pipelines)
- [Total and partial factories](#total-and-partial-factories)
- [The command orchestrator](#the-command-orchestrator)
- [Combinator laws and the async matrix](#combinator-laws-and-the-async-matrix)
- [Property-based testing](#property-based-testing)
- [Value objects](#value-objects)

## Closed hierarchies

The problem is not testing a case. It is that adding a case silently changes nothing — the discard
arm absorbs it, and every existing test still passes. Until closed hierarchies make the switch
exhaustive at compile time (`closed` types are a proposed language feature — re-verify against the
current C# version), the coverage guard has to be a test.

Enumerate the cases once, and assert the enumeration is complete:

```csharp
public static TheoryData<Holding> AllCases() =>
    [ new Holding.Unheld(), new Holding.Held(3), new Holding.Withdrawn(new Holding.Held(1), At) ];

[Fact]
public void EveryCaseIsExercised()
{
    var declared = typeof(Holding).GetNestedTypes()
        .Where(t => t.IsSealed && t.IsSubclassOf(typeof(Holding)));
    var exercised = AllCases().Select(row => row.Single()!.GetType());

    Assert.Empty(declared.Except(exercised));
}
```

Every behaviour that switches over the hierarchy is then a `[Theory]` over `AllCases()`. Adding a
case fails `EveryCaseIsExercised` immediately, instead of reaching `UnreachableException` in
production months later. That single test is most of the value on this page.

Two more claims worth one test each: the hierarchy is genuinely closed (the base constructor is
`private protected`, so a case cannot be declared elsewhere), and the base's static factory maps each
raw input to the case the domain says it should — that factory is where the mapping actually lives,
so it is where the mapping is asserted.

## Result pipelines

Three failures hide in a `Bind` chain, and none of them shows up in a test that only checks
`IsFailure`.

- **The wrong problem.** Assert the case and its payload, as in the skill's example. A refusal
  carrying the wrong currency pair is a bug the caller will act on.
- **The chain runs past a failure.** Give a later step an observable effect — a counter, a spy on a
  dependency you genuinely do not own — and assert it never ran. Short-circuiting is the reason the
  chain exists; nothing else asserts it.
- **`MapError` at the boundary.** A domain refusal reaching the transport layer unrestated means the
  `MapError` is missing. Assert the *layer's own* vocabulary comes out, not the layer below's.

For accumulation, assert the complete set in one unordered assertion, and include a case where two
independent validators fail together. A `TraverseAll` that quietly short-circuits passes every
single-failure test.

## Total and partial factories

The two entry points get opposite kinds of test.

**The partial factory is a table of refusals.** One row per rule, each asserting the specific problem
that rule produces — plus the boundary values on either side of every threshold.

**The total factory is a property.** It has no failure channel, so the claim is that it cannot be
made to break:

```csharp
[Property]
public void FromStorageIsTotal(decimal raw)
{
    var grade = Grade.FromStorage(raw);      // must not throw, for any decimal
    Assert.True(grade.IsWellFormed);
}
```

Generate across the underlying type's whole range, not the domain's — the point of the total factory
is behaviour on values the domain says are impossible.

Then close the loop the property opens. A total factory degrades rather than throwing, so a data
defect becomes a plausible-but-wrong value that no test will ever notice. Every degenerate branch
needs a database check constraint making it unreachable, and **the test that matters is the one
asserting the constraint rejects that row** — an integration test, against the real schema. Without
it, the totality property is a guarantee that failures will be silent.

## The command orchestrator

The safety property of every write path is that a staged failure writes nothing. It is worth one
direct, explicit test per application — not per command:

```csharp
[Fact]
public async Task StagedFailureWritesNothing()
{
    var before = await SnapshotAsync();

    var result = await handler.AddCopyAsync(collector, issue, DraftTheDomainRefuses, ct);

    Assert.IsType<Failure<CopyView, Failure>>(result);
    Assert.Equal(before, await SnapshotAsync());
}
```

**The test is vacuous unless the domain method mutates the aggregate and then refuses.** A refusal
that happens before any mutation proves nothing about the short-circuit — it would pass against an
implementation that saved unconditionally. Pick, or construct, a refusal that comes after the
mutation, and say so in the test name.

Two more assertions belong on the seam rather than on each command:

- **Exactly one `SaveChanges` per command**, counted through an interceptor. Two is two transactions
  wearing one method's name, and it is invisible at the call site.
- **A known constraint violation surfaces as its domain refusal**, and an unknown one propagates.
  A catch-all filter passes the first half of that and fails the second, so assert both.

Where a command uses `ExecuteUpdate` or `ExecuteDelete`, the short-circuit guarantee is void by
construction — those execute immediately. Assert the actual behaviour rather than the pattern's, and
let the test record that the command is not atomic.

## Combinator laws and the async matrix

The monad layer is composed through by everything else, so its bugs are systemic. The laws are cheap:

```
Success(a).Bind(f)            ==  f(a)                          // left identity
m.Bind(Success)               ==  m                             // right identity
m.Bind(f).Bind(g)             ==  m.Bind(x => f(x).Bind(g))     // associativity
```

Add the two that catch real mistakes: `Map` never touches the problem, and `MapError` never touches
the value. Both are one-liners and both have been broken by a copy-paste.

The shapes beyond `Bind` have laws of their own, each a one-liner of the same kind:

```
m.Tap(effect)                 ==  m                             // never changes the value
Failure(p).Tap(effect)        runs no effect                    // never runs on failure
Success(a).Ensure(holds, e)   ==  holds(a) ? Success(a) : Failure(e(a))
```

`Ensure` fails only when the predicate fails, and otherwise passes the value through unchanged.
`TapError` is `Tap`'s mirror: it never changes the problem and
never runs on success.

**A new combinator ships with its laws and a short-circuit test.** Whoever adds a shape to the monad
layer — an `Ensure`, a fold, a `Zip` — adds in the same change the laws that describe it and one test
that, given a failure, the combinator never invokes the work it was handed: the next step's delegate,
the fold's step for the remaining items, the unfold's next page. That test is the claim every caller
relies on, and a combinator that evaluates its delegate before checking the case passes every law
above while doing the work it was meant to skip.

The higher-yield target is the **async matrix**. Its failure mode is not a wrong law — it is one
overload behaving differently from its siblings after somebody filled an empty cell in a hurry. Test
it as agreement rather than as behaviour:

> For each cell, the async form produces the same outcome as the synchronous form it mirrors.

A parameterized test over the cells makes a newly-added overload fail until it agrees, which is
exactly when you want to hear about it.

## Property-based testing

Worth reaching for on algebraic structure, and not otherwise.

**Pays:** round-trips (parse ↔ render, serialize ↔ deserialize, a value converter there and back),
idempotence (normalizing twice equals normalizing once), totality, ordering laws, and model-based
sequences — *any legal sequence of commands leaves the aggregate valid*, which is the highest-value
property a rich domain has and the one nobody writes.

**Does not pay:** business rules with no algebraic structure. "Orders over £100 ship free" is a table
of examples, and a property test over it just re-implements the rule in the test.

Two costs to accept before starting. Writing generators that emit only **legal** domain values is
most of the work — a generator producing invalid states is testing your factory, not your logic, so
be deliberate about which of the two you meant. And failures are seed-dependent: pin the seed, or
record it on failure, or the property becomes a flake nobody can reproduce.

C# has more than one library for this and they differ mainly in how generators compose; the project's
existing choice wins.

## Value objects

A record's structural equality is generated, so it earns no test. Three things about a value object
are not generated and do earn tests:

- **Normalization.** Two spellings the domain considers equal must actually be equal. This is where
  equality bugs live, not in the comparison itself.
- **Comparison operators are total, and the relation had better be.** Where two values can be
  incomparable, `a < b || a >= b` is not a tautology — a property test asserting trichotomy finds a
  partial order wearing a total order's syntax, which is otherwise invisible in the signature.
- **The round trip through persistence**, including nested value objects. Materialization is
  reflection-driven and fails silently; see `ef-core-persistence`.
