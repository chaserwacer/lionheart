# Modeling

Verified against EF Core 10 / Npgsql 10 docs, September 2026. Claims about releases that are not yet GA live in
[Horizon](#horizon-not-yet-ga-re-verify-before-relying-on-it) at the end of this file.

- [Give every meaningful datum its own type](#give-every-meaningful-datum-its-own-type)
- [Strongly-typed IDs — and what they cost](#strongly-typed-ids--and-what-they-cost)
- [Total vs partial construction](#total-vs-partial-construction)
- [Aggregates and cross-aggregate references](#aggregates-and-cross-aggregate-references)
- [The materialization contract](#the-materialization-contract)
- [Complex properties — many types, one table](#complex-properties--many-types-one-table)
- [Row types — when the domain does not fit one mapped shape](#row-types--when-the-domain-does-not-fit-one-mapped-shape)
- [Closed hierarchies](#closed-hierarchies)
- [Inheritance mapping](#inheritance-mapping)
- [Horizon (not yet GA)](#horizon-not-yet-ga-re-verify-before-relying-on-it)

## Give every meaningful datum its own type

The database holds plain data; that is correct and should stay that way. The *model* is where
meaning lives. Work bottom-up, smallest concepts first, so complexity accumulates naturally:
`Currency` → `Money` → `Transfer`.

| Smell | Move | Example |
|---|---|---|
| A primitive with rules | Wrap it | `string` → `Isbn` (13 digits, validated) |
| A primitive a framework type already models | Use the framework type | culture `string` → `CultureInfo` |
| Two or more fields that always travel together | Name the concept | `Title` + `TitleCulture` → `BookTitle` |

The third is the highest-value and the most often missed. If the cluster has a name in the domain, it
is a type.

These types are not data holders. Validation, sanitization, well-known static instances, factory
methods, comparison operators and domain behaviour all belong on them, and none of it interferes with
mapping. A `Timestamp` that refuses non-UTC values, a `TransferId` that refuses `Guid.Empty`, a
`Currency.Usd` singleton — all map fine.

The cost of skipping this is concrete. Primitive obsession pushes validation out of the type and into
every construction site: static factories grow validation helpers, the API handler duplicates them,
the helpers get made `public` so they can be "reused", and the duplication is permanent. Moving the
rule into the type deletes all of it.

Prefer `readonly record struct` for small single-value wrappers on hot paths — it avoids an
allocation per row *and* sidesteps the `ValueComparer` requirement that mutable converted types
carry.

## Strongly-typed IDs — and what they cost

Wrap identifiers: `TransferId`, `ProductId`. Two guarantees, each worth the type alone — passing a
`ProductId` where an `InvoiceId` is expected becomes a compile error, and invalid identity becomes
unrepresentable. Give the type a static factory for fresh values and, where useful, an `Empty`.

Microsoft's own value-conversion docs hedge this pattern: *"Showing this pattern does not mean we
recommend it. Carefully consider whether this level of abstraction is helping or hampering."* Adopt
it knowing the bill:

| Cost | Consequence |
|---|---|
| You cannot query *into* a converted property | `Where(x => x.Id.Value.ToString().StartsWith(...))` does not translate ([efcore#10434](https://github.com/dotnet/efcore/issues/10434)) |
| One converter, exactly one column | A multi-column identity needs a complex type instead |
| `null` never reaches a converter | Nullable handling stays in the mapping, not the converter |
| Mutable converted types need an explicit `ValueComparer` | Without one, change detection silently misses edits |
| Converted parameters are rejected by raw-SQL APIs | `FromSql($"... {id}")` fails; pass `id.Value` |
| Registration must reach foreign keys too | A converter on the key but not the FK column breaks join translation |

Register once in `ConfigureConventions`
(`configurationBuilder.Properties<TransferId>().HaveConversion<TransferIdConverter>()`), never
per-property — shadow foreign keys must inherit the same conversion as the key they point at.

Index-friendliness: a simple `readonly struct → int/Guid` converter is index-transparent. Anything
JSON-serialized is not. EF 10 still emits a no-op `CAST` on some converted columns that blocks index
use, so read the SQL for any converted column an index is meant to serve (see
[Horizon](#horizon-not-yet-ga-re-verify-before-relying-on-it)).

## Total vs partial construction

engineering-standards covers the general rule — a validating partial factory for wire input, a total
one for trusted input. Persistence is where it earns its keep, so two EF-specific consequences:

- **The materialization seam is a trusted-input boundary.** Never call the validating factory on a
  database value: EF materializes rows in bulk, so one malformed row would fail an entire query
  rather than one record. The total factory is what keeps a collection loadable.
- **Every degenerate branch in the total factory needs a check constraint making it unreachable.**
  Without one, a data defect reads as a valid-but-wrong state — silently, and forever, because
  nothing ever throws. That is strictly worse than an exception, and it is the standing cost of
  totality at this seam.

## Aggregates and cross-aggregate references

Aggregate boundaries are consistency boundaries: one aggregate, one transaction, one `SaveChanges`.
Only aggregate roots get a `DbSet`; child entities are reached through the root.

An entity inside one aggregate must not hold a direct reference to an entity in another — that
reference implies the other aggregate is loaded alongside this one, which is what boundaries exist to
prevent. Cross-aggregate references use a **public ID**: a domain-owned, strongly-typed identifier
deliberately separate from the database's key.

From every domain class, delete surrogate keys (`int Id`), foreign keys to the parent, `virtual`
navigations added for lazy loading, public collection setters, and EF attributes (`[Key]`,
`[Required]`, `[MaxLength]`, `[Table]`). Encapsulate collections behind a backing field and a
read-only view, with mutation only through root methods that enforce invariants.

## The materialization contract

This mechanic determines whether a rich model actually persists. Internalize it rather than
trial-and-erroring against it.

EF constructs an instance and then populates it:

- **Constructor binding works only for mapped scalar properties.** If parameter names match mapped
  scalars, EF uses that constructor and skips the setters for those properties. A flat value object
  like `Currency(string Code, int DecimalPlaces)` needs no extra code.
- **Complex-typed constructor parameters are not bound** ([efcore#31621](https://github.com/dotnet/efcore/issues/31621)). Any type whose constructor
  takes a value object cannot be constructed by EF through that constructor.
- **Complex properties are always set through property setters, never the constructor.**
- Given both a parameterless and a parameterized constructor, EF prefers the parameterless one.

So any type holding complex properties needs a **private parameterless constructor** chaining to the
real one with dummy defaults, purely as an EF entry point:

```csharp
public record Money
{
    public decimal Amount { get; private set; }
    public Currency Currency { get; private set; }

    public Money(decimal amount, Currency currency) { /* ... */ }

    private Money() : this(0, null!) { }   // EF entry point only
}
```

Two rules follow:

1. **The dummy-default path must not throw.** Invariant enforcement therefore belongs in static
   factory methods, not constructors. Keep constructors mechanical.
2. **Every property EF must populate needs a setter.** `private set` is fine. A constructor-only
   complex-typed property will not round-trip.

Name the trade-off honestly: this creates a construction path that bypasses validation. The
justification is that data already in the database passed validation on the way in, so
reflection-populated values are valid by induction. That holds when your application is the only
writer. For shared databases, legacy data or external writers the guarantee degrades to "invalid
states cannot be constructed *by application code*" — validate at the load seam and say so out loud.

## Complex properties — many types, one table

For value objects spanning multiple columns, use `ComplexProperty`. **Configurations nest**, which is
what flattens a deep object graph into one table:

```csharp
builder.ComplexProperty(t => t.Amount, amount =>
{
    amount.Property(m => m.Amount).HasColumnName("Amount").HasPrecision(18, 4);

    amount.ComplexProperty(m => m.Currency, currency =>      // nested
    {
        currency.Property(c => c.Code).HasColumnName("CurrencyCode");
        currency.Property(c => c.DecimalPlaces).HasColumnName("CurrencyDecimals");
    });
});
```

**Prefer `ComplexProperty` over `OwnsOne` for value objects.** Owned types are entity types wearing a
costume: they carry hidden identity and reference semantics, so assigning one instance to two
properties fails, LINQ comparison compares identity rather than contents, and `ExecuteUpdate` is
unsupported. Complex types have value semantics, which is what a value object actually is.
Microsoft's guidance is to migrate owned-type value objects to complex types. Reach for owned entity
types only when you want a separate table with its own hidden identity — and then ask whether it is
really a child entity.

**EF 10 limits worth knowing before designing around them:**

- Optional (nullable) complex types are supported, but need at least one *required* property. Where
  the type has no natural candidate, `HasDiscriminator()` supplies one as a required shadow property.
- Collections of structs are unsupported. Collections inside a JSON-mapped complex type are fine.
- Table splitting cannot hold collections; `ToJson()` can.
- Nested complex types cannot bind to constructor parameters.
- TPT/TPC inheritance, and keys/indexes over complex-type members, are **not supported in EF 10** —
  a limit with a horizon, not a permanent constraint (see
  [Horizon](#horizon-not-yet-ga-re-verify-before-relying-on-it)).

`ComplexProperty(..., c => c.ToJson())` maps the whole value object to one JSON column (`jsonb` on
Npgsql 10) — right for opaque payloads read as a unit, or types containing collections. Column
mapping is right when you need to query, filter or index the parts. Decide on read patterns, not
convenience. EF 10 supports `ExecuteUpdate` over JSON-mapped **complex types**, but not over owned
entities.

**EF 10 breaking change, act on it before the first EF 10 migration:** nested complex-type properties
now use the **full path** in column names (`Complex_Nested_Property`, previously `Nested_Property`),
and duplicate complex column names are **uniquified with a numeric suffix**. Both silently generate
rename migrations on upgrade. Pin the existing names with `HasColumnName` if you do not want the
churn. This is the one place the "enrichment is migration-free" promise has an asterisk.

## Row types — when the domain does not fit one mapped shape

Direct mapping is the default and it usually works. It genuinely breaks in four situations, and each
has the same answer: a flat `record` **row type** becomes the EF-mapped aggregate, and the domain
record becomes a read face the row produces through `ToDomain()`.

1. **The domain is an ADT that is not a single-discriminator partition.** A case that *wraps* another
   case — `Withdrawn` around `Base | Variant` — asks one column to carry two independent facts. EF
   gives one discriminator per hierarchy.
2. **The state is computed by the store rather than stored.** When a view derives the discriminating
   column from a join, the entity reads from the view and writes to the table with that column
   stripped from the table's store object.
3. **A bulk loader authors the column list.** Deriving COPY/DDL/upsert column lists from the EF model
   — so there is one author instead of two — requires the mapped type to *be* the column list.
4. **The mapped shape must stay queryable in a way the domain shape is not.** Converter-backed closed
   hierarchies compare as `x.State == Case.Instance`; a type test against them does not translate.

**A lighter shape exists, worth knowing even where row types are the house style.** An ADT whose
cases carry different payloads can stay on the aggregate: map `internal` scalar properties for the
discriminator and the payload columns, expose the ADT as a computed property built from them, and
`Ignore` it. The change tracker keeps seeing the domain object and there is no second type maintained
in both directions — right for a single aggregate whose only awkwardness is one state ADT.

**Where a repo already has a row layer, consistency outranks that.** Match it. Two persistence idioms
in one codebase cost more than either costs alone, and the four situations above become a check on
whether *this* row type is carrying its weight rather than a gate it must pass to exist.

The burden of proof runs one way:

> Map the domain type directly by default. Introduce a row type only for one of the four situations
> above. Any other row type is a DTO tax — pay it deliberately or delete it.

Three rules keep a row layer honest, and all three name ways it commonly fails:

- **`ToDomain()` needs a production caller.** A row layer whose only caller is a test is pure cost:
  the read path re-implements the discrimination inline, once per call site, which is exactly the
  duplication the hierarchy existed to prevent. Either the reads go through it, or the hierarchy goes
  and the rows are named as the aggregates they already are.
- **Name the file for what lives in it.** If behaviour, invariants and refusals sit on the row, the
  row *is* the aggregate. `Model/` holding read faces while `Rows/` holds the aggregate roots reads
  backwards to everyone but its author.
- **`ToDomain()` must be total** — every column combination maps to some case. It therefore degrades
  rather than throws, which means a data defect surfaces as a valid-but-wrong state instead of an
  error. Pair every fall-through with a check constraint so the lenient branch is unreachable rather
  than merely tolerant.

## Closed hierarchies

Model closed state sets as an `abstract record` with `sealed` cases. To persist one, pick by how it
is queried:

- **One scalar column via a value converter** when the set is small and closed and you filter on it.
  This is usually right: it keeps the column indexable and keeps nullable flags out of the schema.
  The converter must be equality-comparable so partial-index predicates translate.
- **A TPH discriminator** when the cases carry different data.

The two forms differ in how you query them, and mixing them up is a common source of empty results:

- **Converter form:** compare against the instance — `x.State == Case.Instance`. A **type test does
  not translate here**, because the converter has already collapsed the hierarchy to a scalar before
  SQL sees it. Nor does an OR of equalities in some providers — use `Contains` over a static array
  of the instances you want. This is the main expressiveness cost of the converter approach.
- **TPH form:** a type test **does** translate, to a predicate on the discriminator column —
  `x is VariantRow ? ((VariantRow)x).VariantName : ""` is a valid projection. Prefer filtering on
  the discriminator property directly when composing further, since narrowing the element type
  mid-query can break translation downstream.

## Inheritance mapping

TPH is the default and the fastest — one table, no joins — and is what a discriminator column buys.
TPC suits sibling-heavy hierarchies queried per type. TPT costs a join per level, so it belongs off
the hot paths. In EF 10, TPT/TPC do not support complex types or JSON mapping (see
[Horizon](#horizon-not-yet-ga-re-verify-before-relying-on-it)).

Discriminator values are part of the schema. Write them as string literals, never `nameof` — a class
rename must not silently rewrite stored data.

## Horizon (not yet GA; re-verify before relying on it)

EF Core 11 is in preview (GA planned for November 2026). Nothing above depends on it; these are
the EF 10 limits it is expected to lift.

- **Complex types under TPT/TPC inheritance**, and **keys and indexes over complex-type members**
  ([Complex properties](#complex-properties--many-types-one-table)).
- **JSON mapping under TPT/TPC** ([Inheritance mapping](#inheritance-mapping)).
- **No-op `CAST`s on converted columns** that block index use are stripped
  ([Strongly-typed IDs](#strongly-typed-ids--and-what-they-cost)).
