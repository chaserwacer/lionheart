---
name: test-design
description: Decides what earns a test and what shape that test takes, for C#/.NET domains built from immutable records, ADT-shaped state, and the Result pattern. Use when writing or reviewing tests, when a change adds behaviour nothing exercises, when tests are brittle, slow, or assert the implementation, or when the question is "what should I test here". Covers the claim a test is making, asserting which failure rather than that one occurred, testing closed hierarchies and total-versus-partial factories, proving a staged failure writes nothing, combinator laws, property-based testing of domain invariants, test data builders for rich aggregates, determinism, and what not to test or mock. Holds no opinion on frameworks, runners, or assertion libraries — it governs design, not tooling.
---

# Test Design

A test's job is to fail when behaviour changes and stay silent when structure changes. Everything
here follows from that: a test coupled to how code is arranged rather than to what it promises is a
refactoring tax, and it will eventually be paid by deleting the test.

Frameworks, runners and assertion libraries are the project's choice, and the sibling test file has
already made it. Match it and move on. The examples here and in the reference are written in xUnit
and FsCheck so they read concretely; the sibling test file's framework wins over them. This skill
governs what to test and what shape the test takes.

| Topic | File |
|---|---|
| The concrete form of each test below — hierarchies, `Result`, factories, orchestrators, laws, properties | `references/functional-shapes.md` |

## What earns a test

Coverage is not the target; a nameable claim is. Write a test when you can state the claim in one
sentence about the domain:

- **A rule the domain asserts** — an invariant, a refusal, a boundary. This is the bulk of it.
- **A behaviour a caller depends on**, at the seam the caller actually uses.
- **A defect, before its fix.** The test that fails first is the only proof the fix works.
- **A total function's totality** — that it yields a valid value for every input its parameter type
  admits, including the malformed ones.

Skip: property getters, `with`-expressions, a record's generated equality, the framework's own
behaviour, and any assertion that restates the implementation line-for-line. A test that must change
every time the method changes is measuring the code, not the promise.

Behaviour this change adds or alters gets its test **in the same change**. A refactor nothing covers
is a change you cannot verify.

## Assert which failure, not that it failed

The commonest weak test in a `Result`-shaped codebase asserts `IsFailure` and stops. The failure
channel carries a typed problem precisely so callers can tell refusals apart — a test that ignores
the distinction leaves that vocabulary untested and passes just as happily when the wrong refusal
comes back.

```csharp
var problem = Assert.IsType<Failure<Transfer, Problem>>(result).Problem;
Assert.Equal(new Problem.CurrencyMismatch(Usd, Eur), problem);
```

The rule runs both ways: a success asserts the **value**, never that it succeeded.

Where a validator accumulates, assert the whole set, unordered, in one assertion. Checking only the
first problem re-creates the exact fail-fast bug that accumulation exists to prevent.

## The tests this architecture needs

Five claims nothing else in the toolchain will catch, each specific to the shapes these standards
mandate. `references/functional-shapes.md` has the form of each.

- **A staged failure writes nothing.** The command seam's short-circuit is the safety property of
  every write path in the application. Assert it directly: run a command whose domain step mutates
  the aggregate and *then* refuses, and assert the database is unchanged.
- **The total factory is total.** A property, not an example: over every input the underlying type
  admits, `FromStorage` returns a valid value and does not throw.
- **Every case of a closed hierarchy is reachable and distinguished.** A `switch` with a discard arm
  compiles no matter how many cases it forgot. Until the language ships closed hierarchies with
  compile-time exhaustiveness (re-verify against the current C# version), a test is the only thing
  watching this.
- **A value object round-trips.** Construct → persist → reload → assert equal, value objects
  included. Materialization breaks silently and nothing else observes it.
- **The combinators obey their laws.** Identity and associativity over `Bind` are a handful of cheap
  tests guarding the layer every other pipeline composes through. A new combinator arrives with its
  laws and a test that it skips its work after a failure.

## Test data

Rich domains make *construction* the expensive part of a test, and that cost is what talks teams into
anemic models. Fix the construction, not the model.

- **One builder per aggregate, valid by default**, with `With…` methods for the single field a test
  cares about. The test then reads as its own premise: `AnEntry.WithNoCopies().Build()`.
- **Name fixtures for the domain state they represent**, not their contents. `AWithdrawnIssue`
  survives a schema change; `EntryWith3Copies` does not.
- **Vary one thing per test.** A builder adjusted in four places has stopped documenting anything.

## Determinism

A test that can fail on a Tuesday is worse than no test, because it teaches the team to re-run.

- **Time is injected.** `TimeProvider` in domain logic, so a test pins the clock and asserts an
  exact instant. A wall-clock read, or a tolerance in the assertion, is the flake waiting to happen.
- **Identity is injected or asserted structurally.** A test that hardcodes a generated `Guid` is
  asserting on the generator.
- **Randomness carries its seed.** A property failure that cannot be replayed is a rumour.
- **Order is asserted only where the domain orders.** Asserting the order of an unordered result is
  the most common flaky test there is.

## The integration boundary

Test against the real dependency wherever the dependency's own behaviour is part of the claim. For a
database that is always: its constraints, its collation, and its translation of your query *are* the
thing under test, and a fake reproduces none of them. The **ef-core-persistence** skill carries the
mechanics and the tiering.

Mock what you do not own and cannot run — a payment gateway, a third-party API. Your own types run
as themselves: a mock of a type you control asserts that you called it the way you wrote the mock, a
tautology that survives any refactor and catches nothing.

## Shape

- **One reason to fail per test.** Several assertions about one outcome are fine; several outcomes
  are several tests.
- **The name states the claim** — the condition and the expected behaviour, in domain words. A name
  that needs the method signature to make sense describes the code rather than the rule.
- **The premise is visible where the test is.** A test whose arrangement lives in a shared setup
  three screens away cannot be read at the point it fails.
- **A suite that has never run is a hypothesis.** Leave the project able to execute, and where the
  code under test has collaborators the tree does not ship, stub them against the shape the code
  implies and say which assumptions you made. A well-designed suite that does not build is worth
  less than a plainer one that does, and the gap is invisible from reading either.
