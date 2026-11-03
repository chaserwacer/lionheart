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

## Personal records — `/api/personal-records`

Read-shaped but for one transition. Records are **never created through a route**: they are
established when a session's sets become final, so `POST /completed`, `POST /{id}/complete` and the
interpretation engine's commit each stage them into the same save as the session itself. The only
write here is the athlete saying a record was not real.

| Verb | Route | Query / body | Answers |
|---|---|---|---|
| GET | `/` | — | every standing record, flat, newest first |
| GET | `/summaries` | — | the standing records grouped by subject |
| GET | `/summaries/{movementDataId}` | — | one movement's standing records, 404 if it is not the athlete's |
| GET | `/history` | `MovementDataId?`, `Kind`, `Bracket?` | one chain, newest first, beaten and retracted links included |
| POST | `/{personalRecordId}/retract` | — | `Restored \| NothingStands` |

A record's **subject** is one movement or trail running, and it crosses the wire as an optional
`movementDataId` — absent meaning trail running — for the same reason program membership does: a
flat shape generates a far better TypeScript client. The domain's two cases are restored at the
service boundary.

An **achievement** is polymorphic on `$type`, one case per kind, each carrying only what its own
presentation needs plus the figures the server derives (an estimated one-rep max, a pace). The
movement itself is **not** embedded — both clients hold a movement catalogue, and the movement is a
different aggregate.

A **rep max** sends its rep count as `reps` and the reps of the set holding it as `setReps`. A set
holds the rep max at its own count and every count below it, so `setReps` is larger when the record
is the front of a longer set ("6RM, from a set of 7"); its `estimatedOneRepMax` is that set's. Rep
maxes run from two to twelve: the one-rep max is the heaviest set, and `/history` refuses `repMax`
with a bracket outside that range.

A movement holds **one record per kind per weight unit**, because weights never convert and the
unit is therefore part of a record's identity rather than a comparability test. A movement logged
in both reads back with a kilograms record and a pounds record of the same kind, each carrying its
own unit inside the achievement. `/history` takes no unit and answers with every unit's chain of
that kind, newest first, which is the progression a screen wants; a client showing one unit filters
on the achievement's own.

A **retraction** answers with a discriminated outcome rather than a nullable record, so a client can
tell "your previous record stands again" from "this subject now holds no record of that kind".
Retracting a record that no longer stands is a `Conflict`, so 409: the same request was legitimate
while it stood.

## Contexts — `/api/contexts`

Two creates rather than one polymorphic body — flat request shapes generate a far better TypeScript
client, and it matches the purpose-built-request rule.

`POST /exertion` (`Period`, `Notes`, `Effort`) · `POST /state` (`Period`, `Notes`) ·
`GET /{id}` · `GET ?from=&to=` · `PATCH /{id}/notes` · `DELETE /{id}`

## Training programs — `/api/training/programs`

`POST /` → `PlannedTrainingProgram` (`Title`, `StartDate`, `EndDate`, `Tags[]`) ·
`POST /{id}/activate` · `POST /{id}/complete` (`Summary`) ·
`GET /{id}` · `GET /` · `PATCH /{id}` (title/dates/tags) · `DELETE /{id}`

## Oura — `/api/oura`

Read-shaped: the native client is the consumer, so each route answers with the stored documents
rather than anything computed. Design and the collections behind it: `docs/oura-integration.md`.

| Verb | Route | Body / query | Answers |
|---|---|---|---|
| GET | `/days?from=&to=` | `DayRange` | one `OuraDayView` per day any collection covered |
| GET | `/days/{day}` | — | that day's `OuraDayView`, or 404 |
| GET | `/sleep?from=&to=` | `TimeWindow` | sleep periods, with stages, hypnogram and both overnight curves |
| GET | `/workouts?from=&to=` | `TimeWindow` | workouts the ring saw or the athlete logged on it |
| GET | `/sessions?from=&to=` | `TimeWindow` | guided meditation, breathing and rest |
| GET | `/tags?from=&to=` | `TimeWindow` | tags the athlete marked |
| POST | `/sync` | `DayRange` | `OuraSyncReport` — per-collection tallies, including what Oura sent that could not be read |

**Two window types, deliberately.** The daily documents are keyed by the calendar day Oura files
them under, so `/days` takes a `DayRange` of `DateOnly`; asking for them in instants would make the
caller invent a time zone for midnight. Events occupy clock time and are windowed on the stamped
`StartTime` column, so they take the same `TimeWindow` contexts and sessions do.

**Absence is absence, not zero.** A collection Oura filed nothing for on a given day is null on the
day view, and a day with nothing at all is missing from a range rather than present and empty.

`/sync` answers 200 with its tallies rather than 204: it is the one write here whose outcome the
caller cannot see any other way. A failure to reach Oura or to authenticate is `Problem.Upstream`,
so 502; a missing stored credential is a `Conflict`, so 409.

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
