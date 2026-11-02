# Comments

Comments are a liability that must earn their keep: they drift, and a drifted comment actively
misleads. Write the fewest comments carrying the most non-obvious information. The bar: would a
strong engineer be grateful this exists, or annoyed to keep it in sync?

## Comment when

- The code is genuinely not self-evident — a clever algorithm, an ordering dependency, a library-bug
  workaround.
- The *why* is invisible: a business rule, an external constraint, a deliberate trade-off, a "looks
  wrong but is correct because…". This is the highest-value comment there is.
- There's a real trap: an invariant, a footgun, "don't reorder these calls".
- A non-obvious design choice needs justification over the obvious alternative — especially when the
  obvious alternative was tried and measured. Recording the measurement is what stops the next reader
  from "optimizing" it back.

## Never write a comment that

- Restates the code.
- Catalogs callers or usages — "called by X" rots on the next move; search is the source of truth.
- Describes another function's or another layer's behavior. Frontend comments don't explain the
  backend.
- Narrates the change just made. "Added null check for…" belongs in the commit message, not in
  `main` — code should read as if it was always that way.
- Runs a paragraph where one sharp sentence would do.

## Prefer better code over a comment

A clearer name, an extracted function, or a named constant usually beats prose. Match the file's
existing comment density and format. Keep tooling-driven doc comments on public APIs (JSDoc, XML-doc)
— those are contracts, not commentary.

## The test

Rewrite or delete if any answer is no:

1. Does it say something the code can't?
2. Will it stay true through nearby refactors?
3. Is it scoped to *this* code, rather than its callers or another layer?
4. Is it as short as it can be while still clear?

## Doc comments on domain types

An XML-doc block on a domain type is the right place for the reasoning behind its *shape* — why three
cases and not six, why this cluster is a record rather than loose members, what a fourth case would
break. That is knowledge no signature carries and no test asserts, and it is the comment most likely
to still be true in a year.

Keep it to the reasoning. A doc comment that re-lists the properties below it has the same problem as
any other restatement.
