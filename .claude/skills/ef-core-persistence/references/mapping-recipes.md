# Mapping recipes

Everything here lives in an `IEntityTypeConfiguration<T>`, one per aggregate, registered with
`modelBuilder.ApplyConfigurationsFromAssembly(...)`. `OnModelCreating` contains nothing else. That
one structural decision makes aggregate boundaries visible in the file layout and keeps every EF
concern in one findable place per aggregate.

- [Value converters](#value-converters)
- [Conventions applied model-wide](#conventions-applied-model-wide)
- [Shadow properties and hidden keys](#shadow-properties-and-hidden-keys)
- [Collections and access mode](#collections-and-access-mode)
- [Views: `ToView`, and the column EF must not write](#views-toview-and-the-column-ef-must-not-write)
- [Keyless entities](#keyless-entities)
- [Generated and computed columns](#generated-and-computed-columns)
- [Check constraints as domain invariants](#check-constraints-as-domain-invariants)
- [Indexes: partial, method, operator class](#indexes-partial-method-operator-class)
- [Defaults as a contract with a loader](#defaults-as-a-contract-with-a-loader)
- [Two contexts over one database](#two-contexts-over-one-database)

## Value converters

For any domain type mapping to a single column, a `ValueConverter` with two lambdas:

```csharp
public class TransferIdConverter : ValueConverter<TransferId, Guid>
{
    public TransferIdConverter() : base(id => id.Value, value => new TransferId(value)) { }
}
```

Use it for strongly-typed IDs, single-value wrappers, framework types (`CultureInfo` ↔ culture name),
and closed hierarchies collapsing to one scalar.

**Information lost in the round trip must be restored explicitly.** A `DateTime` loses its `Kind` in
the database; if the domain type guarantees UTC, call `DateTime.SpecifyKind(value, DateTimeKind.Utc)`
on the way back. Audit every converter for this class of silent loss.

Converters constrain translation — EF cannot translate operations on the underlying value the
converter hides. See the cost table in `modeling.md` before adopting one broadly.

## Conventions applied model-wide

`ConfigureConventions` is where conversions and store types get declared once:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
{
    builder.Properties<TransferId>().HaveConversion<TransferIdConverter>();
    builder.Properties<DateTimeOffset>().HaveColumnType("timestamptz");
}
```

Per-property registration is the bug: shadow foreign keys are declared by string name and never pick
up a converter registered at a usage site, so joins stop translating.

A naming sweep belongs here too — a single pass over the model rewriting property names to
`snake_case`, **skipping any property with an explicit `HasColumnName`** so deliberate names survive.
One sweep shared by every context beats a convention package per context.

**Adding a sweep to a model whose schema is already deployed renames every table and column it
touches.** That is a full rename migration wearing a convention's clothes, and it announces itself
nowhere — the model still validates, the code still compiles, and the diff is one registration line.
Over an existing schema, either pin the current names first or state in the same change that a rename
migration is part of it.

## Shadow properties and hidden keys

The database key stays in the change tracker and the schema, never in a class:

```csharp
builder.Property<int>("Id");
builder.HasKey("Id");
builder.HasOne<Invoice>().WithMany().HasForeignKey("InvoiceId");
```

The domain's public ID then becomes an alternate key:

```csharp
builder.Property(i => i.PublicId).HasConversion<InvoiceIdConverter>();
builder.HasAlternateKey(i => i.PublicId);
```

EF makes it unique and immutable and permits it as a foreign key target, so cross-aggregate
references work against the domain's identity without exposing the database's. A narrow surrogate
stays the physical key; the public GUID is indexed alongside. No meaningful performance penalty.

Accept the sharp edges knowingly: string-named keys give up compile-time safety and rename
refactoring, and reading them in queries needs `EF.Property<T>(entity, "Id")`. That is the price of
persistence ignorance, usually worth paying — but a team choosing otherwise for a given model should
do it explicitly rather than by drift.

Shadow properties also carry store-computed values the domain has no use for — a `tsvector` search
document, a lowercased sort key — keeping them out of the model while remaining queryable via
`EF.Property<T>`.

## Collections and access mode

EF finds conventional backing fields (`_lines` for `Lines`) automatically and uses the field for
read-only collection navigations. Make it explicit where it matters:

```csharp
builder.Navigation(i => i.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
```

Load aggregates whole with `Include` on the command path rather than `AutoInclude` on the model,
which taxes every query that does not need the children.

## Views: `ToView`, and the column EF must not write

Mapping one entity to a view for reads and a table for writes is the way to let the store compute a
discriminating fact — a status derived by joining another schema — without storing it:

```csharp
builder.ToView("entry_v", "app");
builder.ToTable("entry", "app");
```

The view-only column must then be stripped from the *table* store object, and there is no
`PropertyBuilder` overload for it — you go through metadata:

```csharp
var table = StoreObjectIdentifier.Table("entry", "app");
builder.Metadata.FindProperty(nameof(Row.Withdrawn))!.SetColumnName(null, table);
```

**EF diffs a view's columns, never its SQL body.** Changing the `SELECT` produces no migration and
silently no-ops in production. Two obligations follow: ship every redefinition as `CREATE OR REPLACE`
in a new migration, and assert the deployed body against a checked-in expectation
(`pg_get_viewdef`) in a test. This is the highest-drift-risk artifact in a typical EF/Postgres model.

## Keyless entities

A read-only projection entity needs both calls:

```csharp
builder.HasNoKey();
builder.ToView("issue_creator", "catalog");
builder.ToTable((string?)null);
```

The explicit null table is not optional — without it the snapshot scaffolder emits code that throws
on replay.

## Generated and computed columns

Express store-computed columns in the model so EF can diff them:

```csharp
builder.Property(x => x.NormName)
       .HasComputedColumnSql("lower(btrim(name))", stored: true);
```

A generated `tsvector` plus a GIN index gives full-text search that maintains itself:
`HasGeneratedTsVectorColumn(...)` on Npgsql, or a shadow property with
`HasComputedColumnSql("setweight(to_tsvector(...), 'A') || ...", stored: true)` when the expression
needs weighting. Postgres 18 adds virtual generated columns — omit `stored: true`.

Anything expressed this way is model-diffable, so a change generates a migration. That is the
argument for expressing as much SQL in the model as the provider allows, and reserving raw migration
SQL for constructs EF cannot model.

## Check constraints as domain invariants

```csharp
builder.ToTable(t => t.HasCheckConstraint(
    "ck_copy_slab_is_graded", "NOT is_slabbed OR grade IS NOT NULL"));
```

This is what lets a domain type be non-nullable: the database refuses the states the type cannot
express, so a total `FromStorage` never meets them. Every degenerate branch in a materializer should
have a check constraint making it unreachable.

`AddCheckConstraint` **validates against existing rows**. A constraint over a column that already
holds data needs a backfill `UPDATE` in the same migration, ahead of the constraint. Adding one
without a backfill produces a migration that passes on an empty dev database and fails in production.

## Indexes: partial, method, operator class

```csharp
builder.HasIndex(x => x.NormNumber);                                  // deliberately unfiltered
builder.HasIndex(x => x.Name).HasMethod("gin").HasOperators("gin_trgm_ops");
builder.HasIndex(x => x.SortCode).HasFilter("edition = 'base'");
```

Keep the predicate constants that define a partial index in **one shared constant** used by both the
index filter and the queries meant to hit it. A query whose predicate differs textually from the
index filter will not match it.

Filtering an index is not free: a partial index the planner judges unhelpful is worse than an
unfiltered one it uses. Where the choice is non-obvious, measure and record the numbers in a comment
next to the index — that comment is the highest-value kind, because the next reader's instinct will
be to "optimize" it back.

## Defaults as a contract with a loader

`HasDefaultValue` / `HasDefaultValueSql` is not cosmetic when a bulk loader derives its column list
from the EF model: a defaulted column means *the load may omit this*. Make the relationship explicit
by failing the build when a required, undefaulted column has no writer — a small model-walk that
turns a silent `NOT NULL` violation at load time into a compile error.

## Two contexts over one database

When one context must query across a schema another context owns:

- Share the mapping code; re-pin the schema per entity when applying it (`SetSchema` /
  `SetViewSchema`) so the configurations themselves name no schema.
- Mark the borrowed entities `ExcludeFromMigrations` in the borrowing context, so only one context
  authors that schema's DDL.
- Give each context its own migration history table.
- Cross-schema foreign keys should point one direction only, from the dependent schema to the shared
  one — and note that dropping and recreating the shared schema takes those constraints as cascade
  collateral with no pending-migration signal. A startup probe over `pg_constraint` (and
  `to_regclass` for any view you map) turns that into a clear error instead of a runtime failure on
  first write.
