# API surface — specification

Derived from `docs/api-and-composition.md` (§1 request/response shapes, §2 transitions, §3 resume).
Read that first. This file is the route-level spec.

## Principles applied here

- **Requests are purpose-built command records.** Never a mirror of a domain record. They carry only
  what the client is *authorized* to state — never `UserId`, `Provenance`, `Decoration`,
  `DecoratorVersion`, or a server-assigned id.
- **Responses are the domain records** behind one view projection that strips internals
  (`Decoration.SummaryText`, `DecoratorVersion`, embeddings).
- **Transitions, not shapes.** No `PUT /sessions/{id}`.

## Training sessions — `/api/training/sessions`

Lifecycle: `Planned → Active → Completed`, `Planned → Skipped`. Sessions may also be *entered
directly* in any of the three live states, because logging after the fact is the common case.

| Verb | Route | Creates / moves to | Body |
|---|---|---|---|
| POST | `/planned` | `PlannedTrainingSession` | `PlannedFor`, `Prescriptions[]`, `Notes`, `Membership` |
| POST | `/active` | `ActiveTrainingSession` (unplanned start) | `StartedAt`, `Notes`, `Membership` |
| POST | `/completed` | `CompletedTrainingSession` (logged after the fact) | `Sets[]`, `Period`, `Effort`, `Notes`, `Membership` |
| POST | `/{id}/start` | Planned → Active | `StartedAt` |
| POST | `/{id}/complete` | Active → Completed | `Period`, `Effort` |
| POST | `/{id}/skip` | Planned → Skipped | — |
| GET | `/{id}` | — | — |
| GET | `?from=&to=` | — | window over the stamped `StartTime` column |
| PATCH | `/{id}/notes` | any state | `Notes` |
| DELETE | `/{id}` | — | — |

A transition requested from the wrong source state is a **domain refusal** (a `Problem` case), never
an exception and never a silent no-op. `Completed` and `Skipped` are terminal.

### Sets and prescriptions are replaced whole

`PUT /{id}/sets` (Active) and `PUT /{id}/prescriptions` (Planned) replace the entire ordered list.

There is deliberately no per-set route. Sets persist as **one JSON column**, so a whole-list write is
what happens at the storage layer no matter how the route is shaped; per-index endpoints would be an
illusion of granularity over the identical write, and would add index-drift hazards for nothing. The
client already holds the whole session.

## Contexts — `/api/contexts`

Two creates rather than one polymorphic body — flat request shapes generate a far better TypeScript
client, and it matches the purpose-built-request rule.

`POST /exertion` (`Period`, `Notes`, `Effort`) · `POST /state` (`Period`, `Notes`) ·
`GET /{id}` · `GET ?from=&to=` · `PATCH /{id}/notes` · `DELETE /{id}`

## Training programs — `/api/training/programs`

`POST /` → `PlannedTrainingProgram` (`Title`, `StartDate`, `EndDate`, `Tags[]`) ·
`POST /{id}/activate` · `POST /{id}/complete` (`Summary`) ·
`GET /{id}` · `GET /` · `PATCH /{id}` (title/dates/tags) · `DELETE /{id}`

## Interpretation engine — `/api/interpretation`

- `POST /interpret` — `{ RawText, LoggedAt }` → `Committed | AwaitingConfirmation`
- `POST /confirm` — `{ Draft, Choices[] }` → `Committed | AwaitingConfirmation`

Stateless resume per §3: the client echoes back the `ParsedInterpretation` it received together with
its choices, and the server re-resolves **without re-parsing**. Each choice is
`{ RawName, Kind, ChosenEntityId? }` — a null id means *create a new entity under this name*.

`AwaitingConfirmation` is a **success** carrying an outcome, not a failure. It is a 200 with a
discriminated body, not a 4xx.

## Decoration timing — resolve before building `/complete`

`docs/data-remodel.md` settles that sessions are decorated **on completion**. Two things follow that
the implementer must check rather than assume:

1. `InterpretationParser` turns *text* into drafts. Decorating an *already-structured* session is a
   different call and may not exist yet. Establish which it is before designing `/complete`.
2. `InterpretationCommitter` is **create-only** — there is no update path on either the entity or its
   `AnalyzableEntry` projection (a known gap; see that doc's next-steps #2).

If decoration on completion requires the update path, do **not** build it as a side quest. Land
`/complete` writing `NotDecorated`, state plainly in the report that such sessions do not yet reach
`AnalyzableEntry`, and leave it for the roadmap item that already owns it.

## Problem → HTTP, once at the edge

| Problem case | Status |
|---|---|
| not found | 404 |
| unauthorized / not the owner | 401 / 403 |
| invalid request | 400 |
| illegal transition, conflict | 409 |
| upstream (LLM, embedding, Oura) failure | 502 |
| unexpected | 500 |

Ownership is checked on every route: a session belonging to another user is **404**, never 403 —
existence is not disclosed to a non-owner.
