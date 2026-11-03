# Operations

Verified against EF Core 10 / Npgsql 10 docs, September 2026. Claims about releases that are not yet GA live in
[Horizon](#horizon-not-yet-ga-re-verify-before-relying-on-it) at the end of this file.

- [DI, pooling, and context lifetime](#di-pooling-and-context-lifetime)
- [Connection resiliency and transactions](#connection-resiliency-and-transactions)
- [Concurrency](#concurrency)
- [Migrations in production](#migrations-in-production)
- [Hand-written SQL in migrations](#hand-written-sql-in-migrations)
- [Bulk load](#bulk-load)
- [Testing](#testing)
- [Performance checklist](#performance-checklist)
- [AOT and precompiled queries](#aot-and-precompiled-queries)
- [Horizon (not yet GA)](#horizon-not-yet-ga-re-verify-before-relying-on-it)

## DI, pooling, and context lifetime

`AddDbContextPool` / `AddPooledDbContextFactory` is the default for a server application —
Microsoft's benchmark shows 701µs → 350µs and 50KB → 4.6KB per operation. Default pool size is 1024.

Pooling makes the context **effectively singleton in construction**: `OnConfiguring` runs once, so
anything scoped per request (a tenant ID, a user ID) cannot be captured there. Inject it into the
scope that resolves the context instead.

Set an explicit `CommandTimeout`. Pair it deliberately with retry (below) — the two multiply.

Since Npgsql 7 the entry point is **`NpgsqlDataSource`**, not a connection string. Build one per
application with `NpgsqlDataSourceBuilder` — it is thread-safe and owns the pool — and pass it to
`UseNpgsql(dataSource)`. Type mapping, NodaTime, logging and periodic password rotation are
configured there and nowhere else, so a context registered from a bare connection string quietly
opts out of all of them.

Two latency levers live on it:

- **Automatic preparation** (`Max Auto Prepare`, off by default; `Auto Prepare Min Usages`, 5).
  Postgres parses and plans a repeated statement once instead of per execution, and the gain grows
  with query complexity. Cheap and safe for a stable query set.
- **Multiplexing** (`Multiplexing=true`) packs commands from many application-level connections onto
  fewer physical ones, raising throughput under high concurrency. Its costs are structural rather
  than bugs: `BeginTransaction` falls back to a dedicated physical connection for its duration;
  `Prepare()` does not compose, because a prepared statement lives on one physical connection and
  commands move between them; anything relying on session state (temp tables, `SET`) breaks; and
  resultsets read out of send order are buffered whole, which defeats streaming. Turn it on for a
  short-query, high-concurrency service and measure. Leave it off otherwise.

## Connection resiliency and transactions

`EnableRetryOnFailure()` and an explicit transaction do not compose: `BeginTransactionAsync()`
throws under a retrying execution strategy. Wrap the whole unit instead:

```csharp
await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
{
    await using var tx = await db.Database.BeginTransactionAsync(ct);
    // ...
    await tx.CommitAsync(ct);
});
```

Two consequences worth stating out loud in any codebase that relies on them:

- **Retry is only safe while nothing opens an ambient transaction.** That is a load-bearing
  invariant, not an accident — the first person to add `BeginTransactionAsync` gets an exception
  rather than a retry. Say so where the option is configured.
- **Retry forces EF to buffer result sets internally**, which costs memory on large queries.

Retry × command timeout multiply: three retries over a 30s timeout is 96 seconds of a request
somebody is waiting on. Either shorten the timeout or exclude timeouts from the retriable set.

Commit-time connection loss is genuinely ambiguous — the transaction may have committed. The
practical mitigation is `ExecuteInTransactionAsync` with a `verifySucceeded` probe.

## Concurrency

Two defensible strategies. Pick one deliberately and write down which:

**Token-as-invariant.** On Postgres this is `xmin`, not a hand-rolled version column:

```csharp
builder.Property(e => e.Version).IsRowVersion();   // a real uint property, mapped to xmin
```

Npgsql documents this on a **real CLR `uint` property**, not a shadow property — the token has to be
readable to be useful, and the shadow form is undocumented either way. The property need not be named
`xmin`. `UseXminAsConcurrencyToken()` was obsoleted in provider 7.0 and has since been removed; do not re-add it.

There is no `rowversion` equivalent; do not invent one unless you need cross-database portability.
`DbUpdateConcurrencyException` is then the signal.

**Constraint-as-invariant.** Let the database's unique and check constraints define what may exist,
catch the violation, and retry once:

```csharp
catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation
                                  && e.ConstraintName == "ux_copy_owner_issue")
```

Match on `SqlState` **and** `ConstraintName`, map known constraints to domain refusals, and rethrow
anything not on the list — a catch-all here turns a schema defect into a confusing user-facing error.
This suits insert-mostly workloads where the conflict *is* a domain outcome ("already in your
collection") rather than a lost update.

Note that `ExecuteUpdate`/`ExecuteDelete` participate in neither strategy automatically.

## Migrations in production

Microsoft's guidance is organised as a decision table. Four strategies, and what each actually
costs:

| Strategy | For | SQL reviewable | Takes the migration lock | Runs seeding |
|---|---|---|---|---|
| **Migration bundle** | automated deployment — the recommendation | no | yes | yes |
| **Idempotent SQL script** | review-gated or DBA-controlled | yes | **no** | **no** |
| `dotnet ef` CLI | local development | no | yes | yes |
| `MigrateAsync()` at startup | applications that accept the trade | no | yes | yes |

`dotnet ef migrations bundle --self-contained` needs no SDK, no EF tools and no source on the target.
`dotnet ef migrations script --idempotent` is the reviewable alternative, and the two omissions in its
row are easy to miss: scripts take no migration lock, so two concurrent runners are not serialised,
and seeding delegates never run.

`MigrateAsync()` at startup is acceptable — EF 9+ holds a database-wide migration lock, so concurrent
replicas serialise — but it is not the recommendation. A separate deployment step is preferred
"when review, least-privilege credentials, coordinated rollout, or high availability is important."
The objections to reason from: no review point between generating and applying DDL, and it cannot run
inside your own transaction or execution strategy (`MigrationsUserTransactionWarning`).
`EnsureCreatedAsync()` and `MigrateAsync()` are alternatives, never a sequence: `EnsureCreated` builds
the schema with no migrations history table, so a later `Migrate` applies every migration over objects
that already exist.

Three deployment rules from the same guidance:

- **In a container, migrations are a job, not an entrypoint.** Build the bundle at build time and run
  it once as a one-shot job after the database is healthy — not from every replica's startup. Keep the
  SDK and `dotnet ef` out of the application image, and configure the platform not to restart the job
  after a successful exit.
- **Set the environment explicitly on both sides.** Design-time tooling defaults to `Development` when
  neither `ASPNETCORE_ENVIRONMENT` nor `DOTNET_ENVIRONMENT` is set, so a bundle generated or run
  without one can silently load developer user secrets
  ([efcore#36188](https://github.com/dotnet/efcore/issues/36188)).
- **Deploy under a different identity than the application runs under.** The deployment identity may
  change the schema; the runtime identity should hold only the data permissions the application needs.

Two limits before choosing a bundle: it cannot show you the SQL it will execute or list the migrations
it contains ([efcore#25872](https://github.com/dotnet/efcore/issues/25872)), and — like a script — it
updates an existing database, so provisioning is a separate step.

In CI, run `dotnet ef migrations has-pending-model-changes`. EF 9+ throws `PendingModelChangesWarning`
at runtime otherwise, which is a late place to discover it.

Other operational facts:

- EF 10 **reverted** EF 9's "wrap all migrations in one transaction" behaviour.
- EF tools require `--framework` on multi-targeted projects.
- Multiple contexts need `--context`, and each should own a separate history table.
- **Ordering between two contexts' migrations is not enforced by EF.** If one context's migration
  depends on DDL another owns, a shell `&&` is not a guarantee — assert the prerequisite explicitly
  at the start of the dependent migration, or run both through one ordered runner.
- A migration without a `.Designer.cs` has an empty `TargetModel`. Harmless for pure raw SQL, but
  `dotnet ef migrations remove` crossing it restores a wrong snapshot. Generate the Designer or mark
  the migration non-removable in a comment.

## Hand-written SQL in migrations

Normal and expected for constructs EF does not model: views, materialized views, functions, triggers,
row-level security, extensions. Two rules:

- **Make it idempotent where the provider allows.** `CREATE OR REPLACE VIEW`, `CREATE INDEX IF NOT
  EXISTS`, `DROP ... IF EXISTS` in `Down`.
- **Anything EF cannot diff needs an external check.** A view body changed in the model's head but
  not in a migration produces no error and no migration — assert the deployed definition
  (`pg_get_viewdef`) against a checked-in expectation in a test.

A check constraint added over populated data needs its backfill `UPDATE` in the same migration, ahead
of the constraint. Without it the migration passes on an empty dev database and fails in production.

## Bulk load

EF's batched `INSERT` is fine to a few thousand rows. Beyond that, drop to Npgsql binary `COPY`
(`BeginBinaryImport`) — EF has no COPY integration and `ExecuteUpdate` has no insert counterpart.

The staged-table shape for a repeatable full load:

1. `CREATE UNLOGGED TABLE IF NOT EXISTS stage.<t>` (unlogged: the data is reproducible, skip the WAL)
2. `TRUNCATE`
3. `COPY … FROM STDIN (FORMAT BINARY)`, streamed from `IEnumerable<TRow>`
4. **`ANALYZE` the stage table.** Not optional — without statistics the planner nested-loops the
   promote and the load appears to hang.
5. `INSERT … ON CONFLICT (key) DO UPDATE … WHERE <guard>`

The `WHERE` guard on the upsert is what makes a re-run cheap: comparing a source modification stamp
(and value-comparing any column your loader *derives*, since an upstream stamp says nothing about
your transform) means unchanged rows are not rewritten, and unwritten rows generate no WAL.

**Derive the column list from the EF model rather than restating it.** One author for the DDL, the
COPY column order and the upsert set means a new mapped property cannot be silently omitted; add a
model-walk that fails the build when a required, undefaulted column has no writer.

One cost to state honestly: a single transaction spanning a multi-million-row load pins the
cluster-wide xmin horizon, which blocks autovacuum from reclaiming dead tuples in **every** schema,
including ones under concurrent write. If that matters, break the load into per-table transactions or
monitor transaction age during loads.

Deletion policy is a domain decision, not a mechanical one. A mirror that never deletes needs an
explicit representation of "the source stopped carrying this" — a state, not an absence — plus a
guard so a legitimately narrowed source window does not mark everything withdrawn.

## Testing

What to test and how to shape a test is the **test-design** skill's job. This section covers only
what is specific to EF.

Microsoft's *Choosing a testing strategy* is unambiguous: the **InMemory provider is "highly
discouraged"** and "only supported for legacy applications", and mocking `DbSet` gets the same
verdict. SQLite in-memory is closer but cannot exercise provider-specific `EF.Functions`, which for a
Postgres application means `tsvector`, arrays, `jsonb` and trigram all go untested. The
recommendation is to test against the real database system, and the docs name **Testcontainers**
directly as the tooling.

A workable three-tier posture:

1. **Translation tests, no database.** Build a context against an unreachable host and assert
   `ToQueryString()`. Fast, runs in CI, catches plan-shape regressions.
2. **Integration tests against a real Postgres**, ideally Testcontainers so CI gets an ephemeral
   instance rather than a shared developer database. Tests that mutate a shared database and clean up
   in `finally` are a flake and a data-loss risk.
3. **A migration job in CI** that applies every context's migrations end to end against an empty
   database. Migrations that are never applied in CI are untested code that runs once, in production,
   with no rollback.

Round-trip tests per aggregate (construct → save → clear tracker → reload → assert) belong in tier 2;
they are the only thing that catches broken materialization, which fails silently.

Two mechanics the docs settle for tier 2: share one database across a test class through a fixture
with a static initialization lock, and reset write tests by **rolling back a transaction** per test
rather than re-seeding. Where a rollback will not do — a test that must observe committed state —
the cheap alternative on Postgres is to migrate once into a template database and
`CREATE DATABASE x TEMPLATE tmpl` per test.

## Performance checklist

Pooling · `AsNoTracking` on reads · project narrowly · keyset pagination · index-friendly predicates
(`StartsWith` uses an index, `EndsWith` does not) · `ExecuteUpdate` over load-modify-save where its
caveats are acceptable · `EF.CompileAsyncQuery` on hot paths (Microsoft publishes no percentage — its benchmark shows
564 µs vs 672 µs on a simple query, and states the gain grows with query complexity; check what it
cannot carry first — see **Parameterization** in `querying.md`) · never build `Where` lambdas with
`Expression.Constant` (recompiles per call and pollutes the database plan cache).

`dotnet ef dbcontext optimize` (compiled models) cuts first-operation startup for models with
hundreds of entity types. It must be regenerated on every model change, and **does not support query
filters** or lazy-loading proxies.

## AOT and precompiled queries

The official docs label NativeAOT plus query precompilation *"highly experimental… not yet suited
for production use."* Third-party posts sometimes describe it as production-ready; the docs page is
the authority on its status, so read it before acting on any claim that the label has changed. Limits
include no dynamically-composed queries, no LINQ query syntax, and no value converters with captured
state.

`dotnet ef dbcontext optimize --precompile-queries` **without** AOT is the pragmatic middle ground —
startup cost only, no runtime restrictions.

## Horizon (not yet GA; re-verify before relying on it)

EF Core 11 is in preview (GA planned for November 2026). Nothing above depends on it; these are the
limits it is expected to lift.

- **`MigrateAsync()` under an execution strategy.** EF 11 retries transient failures during startup
  migration through an execution strategy of its own. In EF 10 it cannot run under one.
- **Migration ID in the model snapshot.** EF 11 writes the latest migration ID into the snapshot, so
  two branches that each added a migration produce a source-control merge conflict instead of
  diverging silently. Worth the upgrade on any team where that has happened.
