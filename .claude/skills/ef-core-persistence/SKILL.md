---
name: ef-core-persistence
description: Design C# domain models, map them to relational schemas with EF Core 10, and run EF in production. Use whenever any EF Core artifact appears — DbContext, DbSet, IEntityTypeConfiguration, migrations, ComplexProperty, value converters, shadow properties, query translation, pagination — or when work touches aggregates, value objects, strongly-typed IDs, or domain invariants in .NET. Also covers persistence operations — migrations, bulk load, concurrency, pooling, and testing against a real database. Triggers on "the database", "persistence", "save this entity", paging or cursors or streaming a large resultset, or a request for a repository or specification — deciding whether that abstraction should exist is part of the job. Trigger too when a model drifts toward primitives, public setters, or DTO-shaped entities "because of EF Core".
---

# EF Core Persistence

Two claims drive everything here, and both are load-bearing:

1. **EF Core does not require primitive types.** An anemic model is a design failure, not an ORM
   constraint. EF Core maps value objects, strongly-typed IDs, nested complex types, custom
   operators and framework types like `CultureInfo`, and translates queries written against them
   into correct SQL.
2. **EF Core is already a DDD framework.** `DbSet` is the repository. `DbContext` is the unit of
   work. `IEntityTypeConfiguration<T>` is the aggregate definition. LINQ to Entities is the
   specification. Hand-rolled equivalents add code and subtract capability.

The consequence that makes rich modelling cheap: **most enrichment is migration-free.** Replacing a
`string` with a value object, a data clump with a named type, or a `Guid` with a strongly-typed ID
changes the model and leaves the schema byte-identical. A migration appearing during a pure
modelling refactor usually means the mapping is wrong, not the model. (EF 10 added one exception —
see nested complex-type column naming in `references/modeling.md`.)

**Load the `engineering-standards` skill first if it is not already loaded.** This
skill assumes it and deliberately does not restate it: the design survey's *enumerate domain states*
and *name new data clusters* steps produce the model, and everything here covers only how that model
reaches SQL and stays correct in production. Without it you get mapping mechanics with no design
discipline behind them.

Targets **EF Core 10** (.NET 10 LTS) with Npgsql/Postgres. Each reference file states the docs
version it was verified against, and where a later release lifts a limit the file's closing
**Horizon** section says so — a limit with an expiry is not designed around permanently, and Horizon
entries are the ones to re-verify before relying on them.

## Reference index

Everything mechanical lives one level down. Grep before reading, from this skill's own directory —
a bare `references/*.md` resolves against the user's repo and finds nothing:

```bash
grep -rn -B4 "ToView" references/   # run from this skill's own folder
```

`-B4` recovers the section heading and table header, which a bare match drops.

| Working on | Read |
|---|---|
| domain types, value objects, IDs, complex types, inheritance, row types | `references/modeling.md` |
| an `IEntityTypeConfiguration<T>` — converters, shadow properties, views, constraints, indexes | `references/mapping-recipes.md` |
| writing or reviewing a query, or its generated SQL | `references/querying.md` |
| paging, streaming, or otherwise reading a large resultset | `references/querying.md` |
| migrations, bulk load, concurrency, DI/pooling, tests | `references/operations.md` |
| an error message or a wrong result | `references/failure-catalogue.md` |

## The two-pass rule

Domain modelling and persistence mapping are separate activities, done in that order.

**Pass 1 — model the domain, blind to persistence.** No EF using-directives, no attributes, no
surrogate `Id`, no foreign keys, no `virtual` for lazy loading. Reading a domain type should not
reveal whether it is stored relationally, in a document store, or at all.

**Pass 2 — map it, entirely in configuration.** Every accommodation EF needs lives in an
`IEntityTypeConfiguration<T>`, one per aggregate, registered by
`modelBuilder.ApplyConfigurationsFromAssembly(...)`. The mapping layer absorbs the impedance; the
domain never does.

**When the two conflict, the mapping bends.** A mapping constraint is almost never a reason to
change a domain type. Collapsing a domain distinction to simplify a migration is deleting domain
knowledge. Check the mapping options first; raise it with the user second.

Where domain and infrastructure sit in separate assemblies, assert the separation in CI — the domain
assembly referencing `Microsoft.EntityFrameworkCore` is the invariant that decays first. Where one
assembly holds both (common, and fine), the test is per-file instead: no EF using-directive in a
domain type.

## When the domain does not fit one mapped shape

Direct mapping is the default and it usually works. It genuinely breaks in four situations — a second
orthogonal discriminator, a store-computed state, a loader-authored column list, or a mapped shape
that must stay queryable in a way the domain shape is not — and the answer in each is a flat `record`
**row type** that EF maps, with the domain record as a read face it produces. Any row type outside
those four is a DTO tax. The decision is rare and has three common ways to go wrong, so **Row types**
in `references/modeling.md` — the situations in full, the lighter alternative that keeps an ADT on the
aggregate, and the rules that keep a row layer honest — is the place to read before introducing one.

## Write no repositories, no unit of work, no specifications

They already exist:

| DDD pattern | EF Core |
|---|---|
| Aggregate configuration | `IEntityTypeConfiguration<T>`, one per aggregate |
| Repository | `DbSet<T>` |
| Unit of work | `DbContext` |
| Specification | LINQ to Entities |
| Persistence ignorance | Shadow properties |
| Strongly-typed identity | Value converters + alternate keys |

A `Specification<T>` layer reimplements LINQ; a `Repository<T>`/`UnitOfWork` pair reimplements
`DbSet`/`DbContext` with fewer capabilities. Neither is new implementation; both are new maintenance.

If the application layer must not reference EF Core — a legitimate goal — the answer is still not a
wrapper class. Declare narrow, domain-named interfaces in the domain assembly and let `DbContext`
implement them directly. That is code movement, not new implementation. Three rules keep it honest:
no `Update` method (its presence invites fetch-detach-reattach instead of change tracking); never
return `IQueryable` from a domain-facing interface (a leaked queryable is a leaked specification);
return aggregates, not child entities.

The lighter alternative, when there is no such constraint: **`IQueryable<T>` extension methods as a
named query vocabulary**, composed at the call site and terminated by a projection. Same benefit —
the query has a domain name and one definition — without the layer. Scoping predicates that carry a
security guarantee (tenant, owner) belong in a **named query filter** rather than a convention,
because a convention is one forgotten call from a data leak.

## Commands operate on one aggregate; queries cross freely

**Command path** — load one aggregate whole through its root by public ID, mutate through domain
methods, save once. `Include` at the call site rather than `AutoInclude` on the model: aggregates
should load whole on the command path without taxing every query that doesn't need the children.

**Query path** — cross aggregate boundaries as needed, project straight to a read model with
`Select` + `AsNoTracking`, never materialize an aggregate. Never reuse an aggregate as a read model,
and never project with an in-memory mapper over `IQueryable` — that defeats translation.

**The seam between the two is one piece of infrastructure, written once.** A command helper that
takes a `Func<TContext, CancellationToken, Task<Result<T, TProblem>>>`, runs it, returns early on a
staged failure, and only then calls `SaveChangesAsync` gives every command one transaction boundary
and one author. The early return is what makes it safe: a failure returns before the save, so the
context is disposed holding tracked modifications and nothing is written — no rollback, because
there was no transaction. `ExecuteUpdate`/`ExecuteDelete` inside such a body break that guarantee.
The shape, and how constraint violations become domain refusals, is in
the `engineering-standards` skill's `references/command-pipeline.md`.

## Trust the query translator

EF translates far more than most people assume against a rich model: comparison against a whole
complex property becomes equality across all its columns; predicates on nested scalars several levels
deep become the corresponding column predicates; custom comparison operators on a **single-column
converted** value object produce the equivalent SQL comparison.

**Write the query against the domain model first, then read the SQL to confirm.** Assuming it will
not translate — and pre-emptively flattening the model or dropping to raw SQL — is how anemic models
get justified. The one edge worth knowing before you start is that a LINQ to Entities lambda is never
executed, so an operator body contributes nothing to the SQL; `references/querying.md` has the
mechanic, the translation table, and everything that genuinely does not translate.

## Ground truth

The generated SQL is the only authority on what a query does, and `ToQueryString()` reads it without
a database. Assertions over that string catch the failure results-based tests cannot: right answer,
wrong plan.

Two checks are worth naming because they are not obvious:

- **Read the migration diff as a design review.** An unexpected table is an accidental entity; an
  unexpected nullable column is a missing invariant; an unexpected join is a value object mapped as
  an owned type.
- **Round-trip per aggregate.** Construct, save, clear the change tracker, reload, assert equality
  including value objects. Materialization breaks silently, and nothing else catches it.

## When to raise it with the user

- A domain distinction appears to require collapsing to make the mapping work. It almost never does;
  if it genuinely does, that is a domain conversation, not a refactor.
- The database has writers other than this application — the trusted-data assumption behind
  reflection-based materialization no longer holds, and validation must move to the load seam.
- A row layer exists whose `ToDomain()` has no production caller. Wiring the read path through it and
  deleting the inline discrimination is the fix, but it touches every read.
- A comparison on a domain type will not translate and the proposed fix is an expression-tree
  rewriter. Spelling the predicate at the use site is almost always right; the rewriter is a standing
  maintenance obligation. Raise it rather than building one.
- An existing codebase already has a repository/specification layer. Deleting it is right in
  principle and disruptive in practice; propose the direction and let the user scope it.
