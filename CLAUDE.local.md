# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) and other LLMs when working with code in this repository - tailored more specifically to my preferences.

## Overview 
Project Lionheart is a comprehensive athletic training data management platform designed to centralize, contextualize, and analyze the full spectrum of data that influences athletic performance. Unlike traditional fitness applications that focus narrowly on isolated activities or metrics, Lionheart is built around a holistic model of the athlete—capturing training, lifestyle, recovery, and subjective experience in a unified system.

**Athletic performance cannot be meaningfully understood when data is fragmented**. By aggregating diverse data sources into a single, extensible platform, Lionheart enables athletes to view their performance in context.


## Default working standard

- Treat every task as a **production-grade fix or enhancement**, not a quick patch or proof
  of concept. Production quality is the default unless I say otherwise.
- Follow the most modern, idiomatic **Svelte 5** practices — runes and current patterns over
  legacy ones.
- Follow the most modern C#/DotNet coding patterns and practices.

## Comments

When you write or edit code comments, use the **code-comments** skill and follow it.
Short version: comments must be informative, concise, and properly scoped — explain *why* and
the non-obvious, never restate the code, never catalog callers/references, never narrate the
change you just made, and never reach outside their own layer (e.g. frontend comments don't
describe the backend). Default to fewer, sharper comments; when in doubt, make the code
clearer instead of adding a commen1t.

## After writing code — self-review for side effects

When you finish a change, before calling it done, **proactively review how the added or
modified functionality could introduce unforeseen consequences or side effects** — broken
callers, changed assumptions, edge cases, concurrency, data/state implications, regressions in
nearby behavior. Surface what you find honestly; don't wait for me to ask.

Ensure solutions are not unnecessarily complicated. Code should be readable, intelligently designed, bulletproof, 
while remaining as concise and efficient as possible. 

Ensure you follow design practices in the repo: functional result pattern, record heavy models, single responsibility, minimizing null presence, etc.

Gaurd against null presence by leveraging the functional result pattern and encapsulating fill state [empty or present] via encapsulated empty checks for determining presence. 