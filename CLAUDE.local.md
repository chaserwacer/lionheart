# CLAUDE.md

Guidance for Claude Code and other LLMs working in this repository. Portable standards
live in skills; this file holds only what's specific to this repo plus the skill wiring.

## Skill wiring — always

- **Before implementing any nontrivial change**, apply the **engineering-standards**
  skill and run its design survey (sibling search, domain-state enumeration, reuse pass)
  first — not after. Add **ef-core-persistence** when the change touches the database,
  and **test-design** when it needs tests.
- **Before any Swift or SwiftUI work under `ios/`**, apply **swift-engineering-standards**
  and **lionheart-ios** together. The first owns how the Swift is written; the second owns
  where things go in the app, which stock containers to use, and what green means.
- **After completing a change**, run the **code-review-fixer** skill as a final pass and
  report its verdict.
- When I correct a design or implementation mistake you made, offer to log it with the
  **failure-capture** skill.

Skills live in `.claude/skills/` so they travel with the repo and every subagent gets
them. The Swift pair was ported from the comicLoader repo and adapted to this app — the
language rules and the deprecation table carried over unchanged; the architecture,
folders, design system and "green" sections are Lionheart's own.

## This repo

- Stack: native SwiftUI client in `ios/` (the primary client); Svelte 5 PWA in
  `ClientApp/` (desk work); modern C#/.NET backend.
- Design conventions in force: functional result pattern, monadic composition, record-
  heavy models, single responsibility, minimal null presence. Guard against null via the
  result pattern and encapsulated fill-state (empty/present) checks.
- The API is the contract between the two clients. A change to a wire shape touches
  `docs/api-surface.md`, the C# endpoint, and `ios/Lionheart/Core/Model/` in one change.

## The iOS client

The native app is where training actually gets logged, so it leads. It talks to the same
API the web app does, over ASP.NET Identity bearer tokens.

- Deployment target **iOS 18.0**; iOS 26 APIs sit behind `#available` with an iOS 18 path.
  The simulators here run iOS 26+, so the compiler will not catch a missing check.
- **No third-party packages.** Apple's frameworks and the system design language are the
  answer; a dependency needs a case made for it first.
- The project is generated: edit `ios/project.yml` and run `xcodegen generate`. Never edit
  the `.xcodeproj`.


## Worktrees

`.env` lives only in the main checkout. Link it into a fresh worktree before running or
testing anything that talks to LM Studio — run this from inside the worktree:

```bash
ln -sfn "$(dirname "$(git rev-parse --path-format=absolute --git-common-dir)")/.env" .env
```

`--git-common-dir` resolves to the main repo's `.git` regardless of worktree depth, so
this works unedited from any worktree. `.env` is gitignored, so the symlink is never
committed. `.env.example` is the full list. In development the required keys are
`LmStudio__BaseUrl`, `LmStudio__ApiKey`, `LmStudio__Model` and the `Embedding__*` block — the
app throws at startup naming the key if any is missing.

## UX/UI testing

If a change touches UI/UX, use the **/cl-ux** skill. Prompt me before you start testing
so I can start the server.