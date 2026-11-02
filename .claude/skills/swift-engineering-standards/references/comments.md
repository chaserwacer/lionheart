# Comments

Comments are a liability that must earn their keep: they drift, and a drifted comment actively
misleads. Write the fewest comments carrying the most non-obvious information. The bar: would a
strong engineer be grateful this exists, or annoyed to keep it in sync?

## Comment when

- The *why* is invisible: a server behaviour, a platform constraint, a deliberate trade-off, a
  "looks wrong but is correct because…" (`APIClient` setting `Origin` because SvelteKit's CSRF guard
  rejects a native POST without it). This is the highest-value comment there is.
- There is a real trap: an ordering dependency ("apply first, then drop the overlay — never the other
  way round"), an isolation that only holds because of a queue, a SwiftUI overload that misbehaves
  (`GuidedRule` on `Divider().overlay`).
- A choice beat the obvious alternative, especially when the alternative was tried or measured
  (`OutboxUploader` is deliberately not an actor). Recording it is what stops the next reader
  "fixing" it back.
- Something is unsafe by declaration: every `@unchecked Sendable`, `nonisolated(unsafe)` and
  `MainActor.assumeIsolated` names the lock, queue or guarantee that makes it true.

## Never write a comment that

- Restates the code.
- Catalogues callers or usages — "called by X" rots on the next move; search is the source of truth.
- Describes another layer's behaviour. A view's comment does not explain the worker.
- Narrates the change just made. That belongs in the commit message.
- Runs a paragraph where one sharp sentence would do.

## Prefer better code over a comment

A clearer name, an extracted function, a named constant, or a case on an enum usually beats prose.
`// MARK: -` sections are navigation, not commentary; use them where a type is long enough to need
them, and match the file's density.

## `///` doc comments

`///` is DocC markup: the first sentence is the summary Quick Help shows, later paragraphs are the
discussion, and `- Parameters:`, `- Returns:` and `- Throws:` are sections. Write one on:

- **Every type** — what it is, and, for a domain type, the reasoning behind its *shape*: why these
  cases and not others, why "not loaded yet" is a case rather than an optional
  (`EnrollmentSnapshot`). That knowledge is in no signature and no test, and it is the comment most
  likely to still be true in a year.
- **A member whose contract the signature does not carry** — a precondition, which actor it expects,
  what an empty result means, which failures it throws. `- Throws:` names the cases for a
  `throws(E)` function where a caller's handling differs by case.

Apple's API Design Guidelines say to write a documentation comment for every declaration. The house
does not: an app's `private` helper whose name already says what it does needs none, and a `///`
that re-lists the properties below it has the same problem as any other restatement.

## The test

Rewrite or delete if any answer is no:

1. Does it say something the code can't?
2. Will it stay true through nearby refactors?
3. Is it scoped to *this* code, rather than its callers or another layer?
4. Is it as short as it can be while still clear?
