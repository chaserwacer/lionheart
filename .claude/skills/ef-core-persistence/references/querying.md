# Querying

Verified against EF Core 10 / Npgsql 10 docs, September 2026. Claims about releases that are not yet GA live in
[Horizon](#horizon-not-yet-ga-re-verify-before-relying-on-it) at the end of this file.

- [Tracking](#tracking)
- [Project, do not materialize](#project-do-not-materialize)
- [What does not translate](#what-does-not-translate)
- [Custom operators on domain types](#custom-operators-on-domain-types)
- [Named query filters (EF 10)](#named-query-filters-ef-10)
- [Joins](#joins)
- [Include, split queries, N+1](#include-split-queries-n1)
- [Pagination](#pagination)
- [Reading a lot of rows](#reading-a-lot-of-rows)
- [Parameterization](#parameterization)
- [ExecuteUpdate and ExecuteDelete](#executeupdate-and-executedelete)
- [Raw SQL](#raw-sql)
- [Npgsql translations worth knowing](#npgsql-translations-worth-knowing)
- [Testing the plan, not just the answer](#testing-the-plan-not-just-the-answer)
- [Horizon (not yet GA)](#horizon-not-yet-ga-re-verify-before-relying-on-it)

## Tracking

Tracking is on by default. `AsNoTracking` measured ~29% faster with ~39% less allocation in
Microsoft's own benchmark, so every read path should opt out. For read-heavy services, set
`QueryTrackingBehavior.NoTracking` as the context default and opt *in* on the command path instead —
that inverts the failure mode from "silently slow" to "obviously broken".

Use `AsNoTrackingWithIdentityResolution` when a no-tracking query would otherwise duplicate a shared
parent object per child row. Since EF 9 it is *prohibited* on queries that apply LINQ operators
directly to a JSON collection navigation, where it silently returned corrupted results — those take
`AsTracking()` if they need identity resolution and plain `AsNoTracking()` otherwise.

## Project, do not materialize

Queries end in `Select` to a record or an anonymous type. Never reuse an aggregate as a read model,
and never run an in-memory mapper over `IQueryable` — that forces client evaluation of everything
downstream.

Member-initializer projections (`new Foo { A = ..., B = ... }`) keep the result orderable and seekable
by EF; some constructor projections do not. If you intend to `OrderBy` or paginate on a projected
expression, project with member init.

## What does not translate

- **Type tests against converter-backed hierarchies.** `x.State is Withdrawn` does not translate;
  `x.State == Withdrawn.Instance` does. For a set, `StaticArray.Contains(x.State)` — an OR of
  equalities over a converted type frequently fails where `Contains` succeeds. (Type tests against a
  **TPH** hierarchy do translate, to a discriminator predicate — the limitation is specific to
  converters.)
- **Operations on the value a converter hides.** The converter is opaque to the translator.
- **A predicate hidden behind a method call.** EF cannot see through a call into another method, so a
  helper returning a `bool` forces the predicate to be spelled at each use site. When a predicate is
  used in both a filter and a ranking expression, it will be evaluated once per spelling — a real
  cost worth measuring before extracting for tidiness.
- **Narrowing the element type mid-query.** `OfType<Derived>()` in the middle of a composition can
  kill translation of everything downstream; filter on the discriminator property instead.
- **Row-value comparison** (`WHERE (date, id) > (@d, @i)`) is not expressible in LINQ
  ([efcore#26822](https://github.com/dotnet/efcore/issues/26822)); hand-write the OR-expansion.

The rule stays: write it against the model first, read the SQL, then restructure only what actually
failed. Client evaluation is silent — assuming failure is how anemic models get justified.

## Custom operators on domain types

The lambda never runs. EF reads the `BinaryExpression`'s `NodeType` and **ignores its `Method`**, so a
user-defined `>=` is what makes the line compile and contributes nothing to the SQL. Anything the
operator body does beyond comparing — a guard, a normalization, a null policy — is silently absent
from the `WHERE` clause.

| Value object maps to | Ordering (`<` `<=` `>` `>=`) | Equality |
|---|---|---|
| One column via `ValueConverter` | Translates | Translates |
| Several columns via `ComplexProperty` | Throws — nothing to reduce to | Translates, column-wise |

**Spell the guard at the use site** — `Where(x => x.Amount.Currency == usd && x.Amount.Value >= min)`.
It translates today and a reader can see the domain rule.

The multi-column case *can* be taught to EF, by rewriting the expression tree in an
`IQueryExpressionInterceptor`. Treat that as an escalation, not a technique: it leaves you
maintaining two implementations of one semantic — the operator body and the rewriter — with no
compiler link between them, and the fact that the operator is never called is also the reason
nothing will ever report that they disagree. Count the call sites first. Better still, look at
whether the guard belongs in the column layout instead: a discriminated value stored as one column
that means the same thing in every case needs no guard and no rewriter.

## Named query filters (EF 10)

EF 10 removed the one-filter-per-entity limit:

```csharp
builder.HasQueryFilter("Tenant", e => e.TenantId == _tenant)
       .HasQueryFilter("SoftDelete", e => !e.IsDeleted);
// later: query.IgnoreQueryFilters(["SoftDelete"])
```

Any scoping predicate that carries a **security** guarantee — tenant, owner, collector — belongs
here rather than in a convention every read is expected to remember. A convention is one forgotten
call away from a data leak; a filter fails closed.

Two incompatibilities, and both bite exactly where filters are most valuable — on hot paths.
**Compiled models do not support query filters**, so if a model is large enough to want
`dotnet ef dbcontext optimize`, that trade is live. And a filter whose body calls `EF.Constant()` or
`EF.Parameter()` cannot be used from a **compiled query** — see [Parameterization](#parameterization).

## Joins

.NET 10 adds `LeftJoin` and `RightJoin` as LINQ operators, and EF 10 translates them directly.
They replace the `GroupJoin` + `SelectMany` + `DefaultIfEmpty` idiom. Not available in C# query
syntax — method syntax only.

## Include, split queries, N+1

Lazy loading is explicitly discouraged; it is the usual source of N+1. Watch compiled-query cache
hits — since EF 9 that is the `Microsoft.EntityFrameworkCore` meter
(`microsoft.entityframeworkcore.compiled_query_cache_hits` / `_misses`, two counters, no precomputed
rate); the single "Query Cache Hit Rate (%)" figure is the legacy EventCounter read via
`dotnet-counters`. Register the meter with your OpenTelemetry provider or the instruction is
unactionable — if it does not approach 100% after warmup, something is defeating the query cache
(commonly `Expression.Constant` built into a predicate, which recompiles the query every call and
pollutes the database's plan cache).

Multiple collection `Include`s in one query risk cartesian explosion; `AsSplitQuery` avoids it but
costs a round trip per query and internally buffers all but the last result set. Neither is free —
prefer projecting exactly what the caller needs over including whole graphs. EF 10 fixed a real
correctness bug here: pre-EF 10 split queries omitted the key column from the inner subquery's
`ORDER BY`, silently returning wrong rows with `Take()`.

## Pagination

Keyset (seek) pagination is the default; `Skip`/`Take` is the exception that has to be argued for.
Offset makes the database process and discard every skipped row, so page 500 costs 500 pages of work,
and under concurrent writes below the offset the window shifts — entries get skipped and shown twice.
Keyset cost is flat: the same index descent for page 1 and for page 10,000.

Offset is genuinely correct for exactly one thing — **random access**, a UI that jumps to page 47.
Keyset cannot express that. The hybrid is right when both are needed: keyset for next/previous,
offset only on the jump. Ask first whether the jump is used; page numbers are usually a habit rather
than a requirement.

### The predicate

Order by a column set that is **fully unique** — terminate every ordering with the primary key, or
the cursor stalls on a tie or loops. Then compare lexicographically:

```csharp
// ORDER BY Date, Id — the page after (lastDate, lastId)
.Where(p => p.Date > lastDate || (p.Date == lastDate && p.Id > lastId))
.OrderBy(p => p.Date).ThenBy(p => p.Id)
.Take(pageSize + 1)
```

Most databases express this as a row value — `WHERE (Date, Id) > (@d, @i)` — which is shorter and
better optimised. **EF cannot generate it**
([efcore#26822](https://github.com/dotnet/efcore/issues/26822); status under
[Horizon](#horizon-not-yet-ga-re-verify-before-relying-on-it)), so the OR-expansion is hand-written
and stays hand-written. Write it once as an `IQueryable<T>`
extension named for the ordering it implements, not once per call site.

**Mixed directions flip per column and keep the equality chain intact.** For
`ORDER BY Year DESC, Title ASC, Id ASC`:

```csharp
.Where(c => c.Year < lastYear
         || (c.Year == lastYear && c.Title.CompareTo(lastTitle) > 0)
         || (c.Year == lastYear && c.Title == lastTitle && c.Id > lastId))
```

**Fetch `pageSize + 1`** and discard the extra — that answers "is there a next page" without a second
query. Paging backwards is the same predicate with every comparison reversed and the `ORDER BY`
reversed, then the materialised page reversed in memory.

### The index must match the ordering, direction included

A composite index over the ordering columns, in the same order. Direction is not free: PostgreSQL
scans a B-tree in either direction, so `(x, y)` serves `ORDER BY x, y` **and**
`ORDER BY x DESC, y DESC` — but `ORDER BY x ASC, y DESC` is unreachable from it and needs the index
declared `(x ASC, y DESC)` (or exactly its inverse). Mixed-direction sorts are the ordinary case in a
real UI, and the index that looks right is the one that silently forces a sort.

`NULLS FIRST`/`NULLS LAST` is part of that ordering too. A nullable sort key whose null placement
differs between query and index cannot use the index for ordering at all — prefer sort keys that are
`NOT NULL`.

PostgreSQL 18 added B-tree **skip scan**, letting a multi-column index serve queries that place no
restriction on its leading columns. That helps the *filter*; it changes none of the above, because
the ordering still has to come out of the index in the index's own order.

### Cursors

Encode the ordering values into one opaque token: the values, the direction, and a schema version.
Opaque for two reasons — a client that can construct a cursor has made your sort key public API, and
the version is what lets the ordering change later without misreading cursors already in flight
(reject the old version rather than silently reinterpreting it).

The cursor value must compare **identically** to the stored value, and three things break that with
no error at all:

- **Precision.** `timestamptz` is microseconds; a .NET `DateTime` tick is 100 ns. A cursor built from
  an in-memory value that never round-tripped through the database can land *between* two rows.
- **Floats.** A ranked seek on a `double` depends on its text form comparing the same way the column
  does. Scale the rank into an integer and seek on that.
- **Collation.** A `text` seek compares under the session collation; if the index was built under a
  different one, the predicate and the index disagree and rows are skipped silently. Pin the
  collation on the column and build the index with it.

### Total counts

Not by default. `COUNT(*)` over the same predicate is a second full pass, it is the usual reason
"page 1 is slow", and `pageSize + 1` already answers the question the count was standing in for.
Where a magnitude genuinely helps, take an estimate — the planner's row count from `EXPLAIN`, or
`pg_class.reltuples` for an unfiltered table — and label it in the UI as an estimate.

### Paging is not a snapshot

Keyset is stable against inserts and deletes outside the window; it does not give a consistent view
of the whole set. A sweep that must see one version of the data needs an explicit boundary — a
repeatable-read transaction for a short sweep, or a `WHERE CreatedAt <= @sweepStart` cut for a long
one.

## Reading a lot of rows

`ToListAsync`/`ToArrayAsync` buffer the entire resultset. `AsAsyncEnumerable()` with `await foreach`
streams a row at a time, so memory is fixed whether the query returns one row or ten million.
.NET 10 ships `System.Linq.AsyncEnumerable` **in the BCL**, so `Where`/`Select`/`OrderBy` and the rest
compose over `IAsyncEnumerable<T>` with no package — delete any `System.Linq.Async` reference on
upgrade, it conflicts with the built-in one.

That convenience introduced a trap, and it is **specific to .NET 10**. `System.Linq` arrives through
implicit usings, so `ToAsyncEnumerable<T>(this IEnumerable<T>)` is in scope on every LINQ chain — and
since `IQueryable<T>` *is* an `IEnumerable<T>`, and no `IQueryable` overload exists anywhere (EF ships
only `AsAsyncEnumerable`), the call binds it. On .NET 10 that wraps the synchronous enumerator and
runs the query synchronously behind an async signature.

`AsAsyncEnumerable()` is correct on every version and is the fix. The trap is live for the whole of
.NET 10 — the runtime change that removes it, and an analyzer that flags it, are in the next releases
(see [Horizon](#horizon-not-yet-ga-re-verify-before-relying-on-it)).

Two pieces of configuration silently defeat streaming, and a production context usually has both:

- **A retrying execution strategy.** `EnableRetryOnFailure()` makes EF buffer internally so that a
  retry returns the same rows. `ToListAsync` on top of that holds the resultset in memory **twice**.
- **`AsSplitQuery`.** Every resultset but the last is buffered.

"This query streams" is therefore a per-query claim that has to survive the context's own options.

Streaming also pins a connection and an open reader for the whole loop. Anything slow inside the loop
— an HTTP call, a write through a second context — holds a pooled connection for that entire time,
and an open reader cannot be resumed if the process dies. **A keyset page loop is the better
streaming primitive for work that does anything per row**: bounded memory, the connection released
between pages, and a cursor to restart from.

`DbContext` is not thread-safe, so fan-out is `IDbContextFactory<T>` plus `Parallel.ForEachAsync`
with `MaxDegreeOfParallelism` bounded by the connection pool — never concurrent queries on one
context. For many small round trips, one `NpgsqlBatch` beats N commands.

Two escape hatches worth knowing before anyone reaches for raw SQL:

- **Wide rows.** Large `bytea`/`text` values allocate oversize buffers; raise Npgsql's
  `Read Buffer Size`, or read with `CommandBehavior.SequentialAccess` on a hand-written reader.
- **Whole-table extraction.** `BeginBinaryExport` (`COPY … TO STDOUT (FORMAT BINARY)`) is the read
  counterpart of the bulk load in `operations.md`: no materializer, no change tracker, no LINQ. It is
  for export and reporting feeds, not for anything the domain reads.

## Parameterization

EF 10 changed the default for parameterized collections to `MultipleParameters` —
`IN (@ids1, @ids2, …)` with padding — replacing EF 8/9's single JSON-array parameter. This can be a
performance regression on upgrade in either direction depending on cardinality. Override per query
with `EF.Constant()`, `EF.Parameter()`, `EF.MultipleParameters()`, or globally with
`UseParameterizedCollectionMode(...)`.

**Neither `EF.Constant()` nor `EF.Parameter()` survives a compiled query.** Since EF 9 both throw
`InvalidCastException` inside `EF.CompileQuery`/`CompileAsyncQuery` — the reimplementation that stopped
`Constant` from recompiling per value moved it to a pipeline stage compiled queries have already
passed ([efcore#33674](https://github.com/dotnet/efcore/issues/33674)). This is a permanent
constraint, not one with a horizon. Match on the behaviour rather than the message text, because the
wording changes between releases.

The same incompatibility reaches **query filters**
([efcore#38151](https://github.com/dotnet/efcore/issues/38151)), which collides with the named-filter
recommendation above: a hot-path query you meant to compile cannot carry a filter whose body calls
either. Drop the compiled query, or drop the call and let the value go through as an ordinary
parameter. `EF.MultipleParameters()` is new enough that its behaviour here is undocumented — assume it
shares the limit until proven otherwise.

Also new in EF 10: SQL parameter names went from `@__city_0` to `@city`, and inlined constants are
redacted as `?` in logs. The rename breaks SQL snapshot tests and any interceptor parsing
`CommandText`, and will cause a one-time plan-cache recompilation spike after deploy.

## ExecuteUpdate and ExecuteDelete

Fast, and full of sharp edges that are invisible at the call site:

- They **execute immediately** and are **completely unaware of the change tracker**. Mixing them with
  tracked entities in the same unit silently overwrites.
- They **do not open a transaction**. Two calls are two transactions.
- They **bypass `SaveChangesInterceptor`** entirely, reaching only `DbCommandInterceptor`. Any
  audit, outbox or timestamping built on `SaveChanges` is silently skipped.
- No automatic concurrency control — add `Where(x => x.Token == token)` and check rows affected.
- Cannot be batched, single table only, no INSERT counterpart, and `SetProperty` cannot reference
  navigations (project to an anonymous type first).

EF 10 changed `ExecuteUpdateAsync` to take a plain `Func` instead of an expression tree, so ordinary
`if` statements work inside the setter block. Code that built those expression trees stops compiling.

## Raw SQL

`FromSql` / `FromSqlInterpolated` take a `FormattableString` and parameterize safely. `FromSqlRaw`
does not — EF 10 ships an analyzer that warns on string concatenation inside it. `SqlQuery<T>` covers
scalar and non-entity shapes.

Value-converted parameters are rejected by raw-SQL APIs; pass the underlying value.

## Npgsql translations worth knowing

- Full-text: `tsvector.Matches(EF.Functions.WebSearchToTsQuery(...))` — `Matches`, `Rank` (→`ts_rank`)
  and `RankCoverDensity` (→`ts_rank_cd`) are extension methods **on the tsvector value**, while
  `WebSearchToTsQuery` / `PlainToTsQuery` / `PhraseToTsQuery` hang off `EF.Functions`. Over a
  generated `tsvector` column with a GIN index.
- Trigram (`pg_trgm`): `ILike` against a `gin_trgm_ops` index. Remember the escape character when the
  search term may contain `%` or `_`.
- `jsonb` containment, array operators, `NpgsqlCube`, `IPNetwork`.
- **Npgsql 10 breaking change:** `arrayColumn.Contains(x)` now translates to `x = ANY(arrayColumn)`
  instead of `arrayColumn @> ARRAY[x]`. If a **GIN index** backs that column, `@>` used it and
  `= ANY` does not — audit these on upgrade.
- **Npgsql 10:** date/time types now read as `DateOnly`/`TimeOnly` by default rather than
  `DateTime`/`TimeSpan`.

## Testing the plan, not just the answer

`ToQueryString()` renders a query's SQL with no database attached, which makes plan shape assertable
in a fast unit test:

```csharp
Assert.DoesNotContain("JOIN", query.ToQueryString());
```

This catches the failure results-based tests cannot — right answer, wrong plan — and it is the only
practical guard against a refactor quietly reintroducing a self-join or a correlated subquery. Add
one for every hot-path query. It is not a substitute for running against a real database (see
`operations.md`), because it proves nothing about indexes actually being used.

## Horizon (not yet GA; re-verify before relying on it)

.NET 11 and EF Core 11 are in preview (GA planned for November 2026). Nothing above depends on them.

- **Row-value comparison** ([efcore#26822](https://github.com/dotnet/efcore/issues/26822)) is open in
  the backlog with no release attached. The hand-written OR-expansion in [Pagination](#pagination)
  stays.
- **`ToAsyncEnumerable()` over an `IQueryable`.** The .NET 11 runtime
  ([dotnet/runtime#126362](https://github.com/dotnet/runtime/pull/126362)) makes the operator return
  the source unchanged when it already implements `IAsyncEnumerable<T>`, which EF queries do. Not
  backported to .NET 10. EF Core 11 adds analyzer **EF1004** flagging the pattern; the remedy is the
  runtime change, the analyzer is a lint.
- **`EF.Constant()` / `EF.Parameter()` in compiled queries and query filters**
  ([efcore#33674](https://github.com/dotnet/efcore/issues/33674),
  [efcore#38151](https://github.com/dotnet/efcore/issues/38151)) still throw in EF 11; only the
  exception message changes. `EF.MultipleParameters()` has no documented behaviour here yet — assume
  it shares the limit.
