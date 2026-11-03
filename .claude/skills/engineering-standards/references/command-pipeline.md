# Command pipeline

The principle lives in the skill: *complex systems are complex orchestrations of small methods, never
complex methods.* This file is what that looks like when the steps are fallible, asynchronous, and
end in a write — the case where the principle is easiest to state and hardest to hold.

- [The three phases](#the-three-phases)
- [The command seam](#the-command-seam)
- [Why the short-circuit is the whole trick](#why-the-short-circuit-is-the-whole-trick)
- [Constraint violations are domain outcomes](#constraint-violations-are-domain-outcomes)
- [Where retry goes](#where-retry-goes)
- [Reading an orchestrator for defects](#reading-an-orchestrator-for-defects)

## The three phases

A command orchestrator has exactly three phases, in this order, and mixing them is the defect:

1. **Guard** — everything that can refuse before any state is touched. Existence checks, authority,
   request well-formedness. Cheap, read-only, and each returns a `Result` so the chain short-circuits.
2. **Stage** — load the aggregate, mutate it through domain methods. Still no write. Every refusal the
   *domain* makes surfaces here, restated into this layer's vocabulary with `MapError`.
3. **Commit** — one `SaveChanges`, once, at the end.

A guard that drifts below a stage still refuses — with the **wrong reason**. An order that is both
already fulfilled and short of stock gets told it is short of stock, because the cheap check ran after
the expensive one. Every guard sits above the first load, and among themselves they run in the order
the caller most needs to hear.

```csharp
public Task<Result<CopyView, Failure>> AddCopyAsync(
    CollectorId collector, IssueId issue, CopyDraft draft, CancellationToken ct) =>
    catalog.ExistsAsync(issue, ct)                                    // 1. guard
        .BindAsync(_ => CommandAsync(async (db, token) =>             // 3. commit wraps 2.
        {
            var now = clock.GetUtcNow();
            var entry = await EntryAsync(db, collector, issue, token)
                        ?? Start(db, collector, issue, now);          // 2. stage
            return entry.Acquire(draft, now)                          // the domain may refuse
                .MapError(Failure (EntryProblem p) => new Failure.Refused(p))
                .Map(copy => CopyView.Of(copy));
        }, ct));
```

The orchestrator names the steps and chains them. It performs none of them. If you can see a `WHERE`
clause, a column name, or a validation rule in the top-level method, a phase has leaked upward.
Inside the body, statements are fine where no result is being taken apart — the `await … ??` finds or
starts an entity — and the fallible domain step stays on the track.

## The command seam

The seam where `Result` composition meets the unit of work is the one piece of infrastructure worth
writing once per application. It is small, and every command goes through it:

```csharp
private async Task<Result<T, Failure>> CommandAsync<T>(
    Func<AppDbContext, CancellationToken, Task<Result<T, Failure>>> command,
    CancellationToken ct)
{
    await using var db = await contexts.CreateDbContextAsync(ct);

    var staged = await command(db, ct);
    if (staged is not Success<T, Failure>) return staged;    // nothing is written

    try
    {
        await db.SaveChangesAsync(ct);
        return staged;
    }
    catch (DbUpdateException e) when (Refusal(e) is { } refusal)
    {
        return Result<T, Failure>.Failure(refusal);
    }
}
```

What it buys, and why each part is load-bearing:

- The command body gets a context and a token and returns a `Result`. It never saves. **One author
  for the transaction boundary** instead of one per command.
- A pooled factory (`IDbContextFactory<T>`) rather than an injected context, so the lifetime is the
  command's rather than the request's. See
  the `ef-core-persistence` skill's `references/operations.md` on pooling.
- The `catch` filter converts *known* constraint violations to refusals and lets everything else
  propagate — a catch-all here turns a schema defect into a confusing user-facing error.

## Why the short-circuit is the whole trick

`if (staged is not Success<T, Failure>) return staged;` looks like a formality. It is the reason the
pattern is safe.

This check is one of the few places allowed to inspect a result's case by hand, because the seam is
where the railway meets the unit of work and it is written once
([Stay on the track](railway-orchestration.md#stay-on-the-track) has the full list). It is not a
precedent for an orchestrator.

A staged failure returns **before** `SaveChangesAsync`, so the context is disposed still holding
tracked modifications and **nothing is written**. There is no rollback, because there was never a
transaction to roll back. The change tracker is a staging area you are free to abandon, and the
`Result` failure is what abandons it.

Two consequences worth stating out loud:

- **A domain method that mutates the aggregate and then returns a failure is correct**, not sloppy.
  The mutation dies with the context.
- **`ExecuteUpdate` / `ExecuteDelete` break this.** They execute immediately and are unaware of the
  change tracker, so a later staged failure cannot un-write them. Either keep them out of a command
  body, or accept that the command is no longer atomic — and say which.

## Constraint violations are domain outcomes

For insert-mostly work, the conflict *is* an answer to the user's question ("that folder name is
taken"), not a lost update. Match on the specific constraint, map it to a refusal, and rethrow the
rest:

```csharp
catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation
                                  && e.ConstraintName == FolderNameIndex)
```

Match on **both** `SqlState` and `ConstraintName`. Matching on the state alone maps every future
unique index onto whichever refusal was written first, silently, the day someone adds one.

Keep the constraint name in a shared constant used by both the mapping configuration and this filter,
so a rename cannot leave the two disagreeing.

## Where retry goes

Retry belongs **around the whole staged command**, never around `SaveChangesAsync` alone — a retry
that reuses a context whose change tracker already applied the mutation will apply it twice. Re-create
the context, re-stage, re-save.

Bound it. One retry answers genuine contention; an unbounded loop turns a permanent conflict into a
hung request. If the second attempt fails the same way, rethrow — the conflict is not contention.

Where the provider's own resiliency is enabled, an explicit transaction and `EnableRetryOnFailure`
do not compose; see the `ef-core-persistence` skill's `references/operations.md`.

## Reading an orchestrator for defects

Seven questions to ask of any orchestrator:

1. **Can a step's failure leave state written?** Anything that escapes the change tracker —
   `ExecuteUpdate`, a raw command, an HTTP call, a queue send — is a step that cannot be abandoned.
   It belongs after the commit, or behind an outbox.
2. **Is every guard above the first load?** A cheap refusal evaluated after an expensive one hands
   the caller the wrong reason for the same rejection, and no test that checks only "it refused"
   will see it.
3. **Does the top-level method perform as well as sequence?** A `Where` clause or a business rule
   inline in the orchestrator is a phase that leaked.
4. **Is there more than one `SaveChanges` in one command?** That is two transactions wearing one
   method's name.
5. **Is `ct` threaded, or captured?** A step that ignores the token cannot be cancelled, and the
   request outlives the caller.
6. **Does the failure vocabulary change at the boundary?** A domain refusal reaching the transport
   layer unrestated means a `MapError` is missing, and the transport now depends on domain internals.
7. **Does it stay on the track?** Any place outside the monad layer and the edge that inspects which
   case a `Result` is — a case test, a cast, a switch over the cases, a mid-chain `Match` that
   rebuilds a result, a loop returning `Failure` early — is a combinator written out by hand. See
   [Stay on the track](railway-orchestration.md#stay-on-the-track).
