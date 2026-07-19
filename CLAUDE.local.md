# CLAUDE.md

Guidance for Claude Code and other LLMs working in this repository. Portable standards
live in skills; this file holds only what's specific to this repo plus the skill wiring.

## Skill wiring — always

- **Before implementing any nontrivial change**, apply the **engineering-standards**
  skill and run its design survey (sibling search, domain-state enumeration, reuse pass)
  first — not after.
- **After completing a change**, run the **code-review-fixer** skill as a final pass and
  report its verdict.
- When I correct a design or implementation mistake you made, offer to log it with the
  **failure-capture** skill.

## This repo

- Stack: Svelte 5 (runes, current idioms only) frontend; modern C#/.NET backend.
- Design conventions in force: functional result pattern, monadic composition, record-
  heavy models, single responsibility, minimal null presence. Guard against null via the
  result pattern and encapsulated fill-state (empty/present) checks.


## UX/UI testing

If a change touches UI/UX, use the **/cl-ux** skill. Prompt me before you start testing
so I can start the server.