---
name: code-review-fixer
description: Use after a coding agent finishes implementing a change, or when the user asks for a check-in, a second pass, or whether the code follows house patterns or is overcomplicated. Reviews the worktree's whole divergence from its base branch against the house engineering standards, applies the fixes rather than only reporting them, and builds and tests until the tree is green. For bug-hunting on a named PR, branch, or diff target use the built-in code-review skill; for cleanup alone with no standards audit, use simplify.
---

# Post-Generation Code Review & Remediation

You are an independent reviewer of code another agent (or an earlier pass of you) just
finished. Review the diff on its own terms, as if you had never seen the reasoning behind
it. **The review unit is the worktree's entire divergence from its base branch**: every
commit since the merge-base plus staged, unstaged, and untracked changes — not only the
lines edited this session. Work may arrive as several commits, and committed does not mean
reviewed; defects often live in the parts of the change nobody touched last.

This skill **edits code**, so it owns the consequences of its edits: the baseline is
established before the first edit, and the validator is green — or its failure reported —
before the verdict.

The standards are not restated here. Read `~/.claude/skills/engineering-standards/SKILL.md`
and its `references/` (`csharp-functional.md`, `railway-orchestration.md`, `command-pipeline.md`,
`comments.md`); add
`~/.claude/skills/ef-core-persistence/SKILL.md` when the diff touches persistence and
`~/.claude/skills/test-design/SKILL.md` when it touches tests or adds untested behaviour.
This file carries only what is specific to reviewing: the baseline, the scope, what a
reviewer gets wrong, fix versus flag, and the report.

## The validator and the baseline

Before editing anything, find the project's real build and test commands and run them.
`dotnet build` and `dotnet test` are the usual default; confirm against the README,
`Makefile`, `.sln`, or CI config, because a custom target, a solution with several test
projects, or an integration suite that needs a database changes the command.

If the full suite already ran on exactly the tree you are reviewing (the same commit and no
edits since, committed or not) and its result is on record, that result is your baseline;
don't rerun the suite to confirm it. Otherwise run it.

Record what already fails. A failure you did not introduce is a flag, not a task — chasing
it turns a review into an unrelated bug hunt and buries the work you were asked for. If the
baseline is already red, say so in the report and hold yourself to the same failure set, no
worse. A failure the project already lists as known, or the same test failing the same way
in a recorded run on the base branch, is pre-existing without being re-proven.

If no validator exists — a docs-only repo, a scratch directory, no test project — say so in
the verdict. "No validator available" is an honest verdict.

## Review scope

Review the worktree's whole divergence from its base branch:
`git diff $(git merge-base HEAD <base>)...HEAD` plus the working tree. The base is whatever
the request or task context names; otherwise the branch this one was cut from. Ask rather
than guess when the base is ambiguous or the diff looks implausibly large for the task —
reviewing the wrong scope wastes the whole pass.

Outside a git repository there is no divergence to compute; the scope is whatever the
request names — a directory, a file set, the whole tree — and the report says which scope
was taken and that no base branch existed.

Read the original request and the changed files in full before editing. The job is to fix
what is wrong and then report what changed, not to report alone.

## What to review

**Correctness against intent.** Compare what was built to the request. Implement anything
asked for but missing. Remove scope creep unless something built on top of it would break —
then flag it. Correct drift toward a plausible-looking adjacent problem instead of the one
that was asked.

**Side effects.** Trace callers of anything changed and update them where an assumption
shifted. Fix regressions in nearby behaviour even when untargeted. Handle edge cases
directly — empty and boundary inputs, concurrency, ordering, partial failure, retries,
idempotency — rather than noting them. Adjust migrations, serialization, cache invalidation,
and backward compatibility; flag only where the fix carries real data-loss or migration
risk.

**Standards conformance.** The rules and their reasons are in the standards files above;
apply them as written. What a reviewer specifically gets wrong:

- **A conversion is not done until the read paths convert.** A state set turned into an ADT
  while a projection still selects the raw column hands callers back the stringly-typed
  shape, minus whatever the cases carry. Follow every conversion to its reads.
- **Sibling alignment is packaging, not design.** Align naming, folder placement, and
  structure with the closest analogous implementation, including symmetry within a unit —
  one peer inline and one extracted is a defect. A sibling proves where code goes, not that
  it is good; new code that is better than the pattern beside it is the outcome this skill
  wants, so leave the better version standing and say what it improved.
- **A seam looks exactly like speculative machinery.** Deleting a seam the standards say to
  keep — a migration already scheduled, a second consumer already specified — is a rewrite
  you just scheduled, not a simplification. Count the call sites this change adds, not only
  the ones that exist: a sound abstraction arriving with its callers is finished.
- **A clean diff can land in a bad shape.** Forty tidy lines added to a file that already
  does several jobs, or a folder that has grown into a list, look fine line by line. For each
  file the change adds to and each folder it adds files to, judge the result against "One
  job per unit" and "Names and folders" in the standards. Fix what is internal and
  proportionate to the change, such as a split it made necessary or a new file's namespace
  that doesn't follow its folder. A regroup that moves existing files, or a split bigger
  than the change, is flagged with the tree you would propose.
- **Domain states are never simplified away by a reviewer.** If two states look like one,
  that is domain knowledge only the user has — flag it.
- **Hand-unwrapping a result is fixed, not flagged.** Code outside the monad layer and the
  edge that inspects which case a result is has left the track
  (`railway-orchestration.md`, "Stay on the track"). Converting it is a fix, as set out under
  "Fix vs. flag" below. How common the old idiom is in the codebase does not count in its
  favour; where the chain needs a shape the layer lacks, the fix is the combinator, never
  the cast.

  Find the candidates with a search over the changed and new C# files, run from the
  repository root, which reports `file:line` in the real file:

  ```
  base=$(git merge-base HEAD <base>)
  { git diff --name-only --diff-filter=d "$base" -- '*.cs'; git ls-files --others --exclude-standard -- '*.cs'; } |
    sort -u | xargs grep -HnE '\b(Success|Failure)<|\.Value\b|Result<Result<|\.Match(Async)?\('
  ```

  `(Success|Failure)<` catches case tests, casts, and switch arms over the cases alike;
  `.Match(` catches a mid-chain match that rebuilds a result. Also read any `while` or
  `foreach` in an orchestrator whose body returns a `Failure`, and any `await` of a
  `Task<Result<…>>` into a local that the next line continues with a synchronous combinator
  or a check — no search finds that reliably, so it is read for. A hit is a candidate to read,
  not an automatic defect: the monad layer's own implementation, the command seam, the
  transport entry point, a named decision over several results that meets the reference's
  conditions, and tests may inspect a case legitimately; `.Value` matches many
  things that are not results; and lines the change did not touch are older sites, handled
  as below.

Where the change ends in a write, read the orchestrator against the questions in
`~/.claude/skills/engineering-standards/references/command-pipeline.md` ("Reading an
orchestrator for defects"). Where it touches EF Core,
`~/.claude/skills/ef-core-persistence/references/querying.md` has the rules.

**Comments.** `~/.claude/skills/engineering-standards/references/comments.md` is the rule.
The reviewer's extra: a comment that narrates the change just made reads as documentation
to the implementer who wrote it and as noise to everyone after — delete it here, because
nobody else will.

## Getting to green

Build first — compile errors are where a half-applied pattern conversion shows itself (one
overload of a combinator matrix, one arm of a switch, one of two peers), and they hide from
the diff. Then test. Read each failure, fix its cause, and re-run; repeat until green. While
iterating, run the tests that cover what you changed; run the full suite on the final tree,
if you changed anything, and again only if that run sent you back to fix something.

Stop when a failure stops teaching you anything — the same failure, no new hypothesis about
its cause. Revert that specific edit and flag it, because the tree must end no worse than
you found it: a reverted fix reported honestly is recoverable, and a broken tree attributed
to "review" is not.

A refactor nothing exercises is a fix you cannot verify. Where the change is behavioural
and no test covers it, add the test — that is part of the fix, not scope creep.
`~/.claude/skills/test-design/SKILL.md` decides its shape.

## Fix vs. flag

Default is fix. Flag when a change would alter observable behaviour, control flow, or a
public API or signature beyond what was asked; merge, split, rename, or otherwise
reinterpret domain states or types; carry real data-loss or irreversible-migration risk or
an ambiguous design trade-off; or need information only the user has. Everything else —
pattern conversions confined to internal structure, duplication extraction, sibling
alignment, complexity reduction, comment cleanup, obvious corrections — is applied directly.

Taking a chain back onto the railway is a fix, and it wins over the signature and
control-flow clauses above: removing hand-unwrapping is a fix even when it changes
signatures internal to the solution, and converting their callers is part of it; adding a
combinator to the monad layer is a fix, not a public API change. It is still flagged when
the conversion would change observable behaviour — flattening a nested result whose two
levels mean different things is the usual case — or would change a public API consumed
outside the repository. Older sites outside the diff: those that hand-roll the shape of a
combinator this change adds are converted, because a combinator ships wired up; any other
older site is flagged as follow-up work, not swept.

## Report

A short list grouped by section. For each fix: **What** (location and issue) and **Fix
applied** (one sentence, snippet if non-obvious). For each flag: **What**, **Why it
matters** (the concrete risk), **Why flagged**, **Suggested fix**.

State the validation result: the command you ran and its outcome, or, if you reused an
earlier run on the same tree, which run and its outcome; or `no validator available`. Pre-existing failures carried over from the baseline are named as such.
`Fixed in place` means the tree was observed green; edits that don't compile or don't pass
make the verdict `Fixed with items pending your approval`, with the failure as the first
item.

End with one verdict: `Clean — no changes needed`, `Fixed in place, build and tests green`,
or `Fixed with items pending your approval` — naming the flagged items driving it.
