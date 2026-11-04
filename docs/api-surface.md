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
| POST | `/active` | `ActiveTrainingSession` (unplanned start) | `StartedAt`, `Notes`, `Membership`, `RoutineId?` |
| POST | `/completed` | `CompletedTrainingSession` (logged after the fact) | `Sets[]`, `Period`, `Effort`, `Notes`, `Membership`, `RoutineId?` |
| POST | `/{id}/start` | Planned → Active | `StartedAt` |
| POST | `/{id}/complete` | Active → Completed | `Period`, `Effort` |
| POST | `/{id}/skip` | Planned → Skipped | — |
| POST | `/{id}/revise` | Completed → Completed (corrected) | `Sets[]`, `Period`, `Notes` |
| GET | `/{id}` | — | — |
| GET | `/{id}/recording` | — | a workout imported from Apple Health: route, splits, heart rate, the app's metrics (see "Apple Health") |
| GET | `?from=&to=` | — | window over the stamped `StartTime` column |
| PATCH | `/{id}/notes` | any state | `Notes` |
| DELETE | `/{id}` | — | — |

A transition requested from the wrong source state is a **domain refusal** (a `Problem` case), never
an exception and never a silent no-op. `Completed` and `Skipped` are terminal; a completed session
can still be **corrected** in place.

**Revising a completed session.** `POST /{id}/revise` restates the sets, the period and the notes
whole and answers with the session; the effort, the program and the routine stay as they were. Any
other lifecycle state is a 409. A set the revision leaves as it was keeps the source and facets it
was stored with, and a start left where it was keeps the offset the session was logged at — the
day is read at that offset, and a client restates the period at its own. When the sets change or the start moves, the athlete's records are re-detected over
his whole history in the same transaction (the `rebuild-records` replay), because a record is judged
against what stood before it; a notes-only revision or a moved end touches no record. Such a
revision also drops the session's decoration and its analysis projection, which described the old
sets. While any record is withdrawn the replay would award it again, so a revision that reaches the
records is then a 409 saying so, and nothing is written. The replay writes every record afresh, so
any revision that reaches the records gives every record a new id; a client drops the record chains
and marks it holds and reads them again.

**Routine origin.** Every session view carries `routineId` beside `trainingProgramId`: the routine it
was trained from, or `null` when it was ad hoc. `POST /active` and `POST /completed` take an optional
`routineId`. Unlike a program id, a routine id that is not one of the athlete's routines — deleted
since, or never theirs — is **dropped to ad hoc, never refused**: a workout started offline from a
routine deleted meanwhile still happened, and must land. A planned session is ad hoc, and every
transition carries the origin over. Deleting a routine leaves its sessions naming it.

**Session source.** Every session view carries `source`: `$type` `athleteLog` (entered in Lionheart,
or imported from the athlete's own logs) or `appleHealth` with `workoutId` (the workout's Health
UUID) and `recordedBy` (`name`, `bundleId` — the app that wrote it to Health). The server sets it;
no request states it.

**Set kind.** A lift set carries `kind` (int): `0` working, `1` warm-up, `2` drop, `3` failure. A lift
sent without one is a working set, and a stored lift written before sets had a kind reads as one.
Only lifts carry a kind. A warm-up never sets a record; drop and failure sets count as working sets
do.

### Previous performances — `GET /api/training/previous-performances`

One entry per movement the athlete has a completed set on: `movementDataId`, the `trainingSessionId`,
`date` and `startedAt` of the most recent completed session that worked it — a session that only
warmed up on it is passed over (most recent by the stamped start time), and `sets` — that movement's sets from that session in performed order, warm-ups
included with their kind. Sets with no movement (trail runs, endurance sets, freeform) never appear. The list's order
carries no meaning. It reads every completed session, since which sessions hold a movement lives in
the sets' JSON column.

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
| GET | `/established` | `MovementDataId?`, `From?`, `To?` | every record set on that movement and/or achieved inside `[From, To]`, newest first, in every standing; 404 if the movement is not the athlete's, 400 if `To` precedes `From` |
| POST | `/{personalRecordId}/retract` | — | `Restored \| NothingStands` |

**Established records** are what a client marks sets with. Each filter is optional and an absent
one filters nothing — unlike `/history`, where an absent movement means trail running, so with no
movement the answer includes trail-running records. `From` and `To` are instants and both ends are
included. A record's `achievedAt` is its session's start in UTC, so a session's records are the
window `[start, start]`; a record does not name the session that set it, so that instant, the
movement and the achievement's own figures are how a client finds the set. Beaten and retracted
records come back too: a set that took a record still took it after a later session beat it.

A record's **subject** is one movement, trail running, riding or hiking. It crosses the wire flat,
for the same reason program membership does — a flat shape generates a far better TypeScript
client: an optional `movementDataId`, and `subject` (`movement` | `trailRunning` | `riding` |
`hiking`) on every record and every subject's list. `/history` takes the same pair: a
`movementDataId`, or `subject=riding` / `hiking` / `trailRunning` with none; neither is trail
running, as it was before riding and hiking had records. The domain's cases are restored at the
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

## Wellness — `/api/wellness`

Pre-remodel routes (`Endpoints/WellnessEndpoints/`), kept as they are: a wellness state is the
athlete's own daily rating, one per day.

| Verb | Route | Body / query | Answers |
|---|---|---|---|
| POST | `/add` | `{ date, energy, motivation, mood, stress }`, each rating 1–5 | 201 for a new day, 200 when the day's entry is replaced; the `WellnessState` |
| GET | `/get-range?startDate=&endDate=` | `DateRangeRequest`, both ends included | the day's entries, oldest first |
| GET | `/get?date=` | — | the day's entry, or a placeholder with `overallScore: -1` |

`date` is a `DateTime` whose calendar day is the key; the client sends midnight of the day. A
rating outside 1–5 is a 400 naming the field. `overallScore` is the server's mean of the four with
stress inverted (`6 − stress`), to two places.

## Routines — `/api/training/routines`

A routine is an undated, reusable order of movements, each with a working-set count. Targets are not
stored on it: the client reads them from previous performances.

| Verb | Route | Body | Answers |
|---|---|---|---|
| POST | `/` | `{ name, notes, movements: [{ movementDataId, workingSets }] }` | 201 `RoutineView` |
| POST | `/from-session/{sessionId}` | `{ name }` | 201 `RoutineView` |
| GET | `/` | — | `[RoutineView]`, oldest `createdAt` first |
| GET | `/{id}` | — | `RoutineView`, or 404 |
| PUT | `/{id}` | same body as `POST /` | `RoutineView`, name, notes and movements replaced |
| DELETE | `/{id}` | — | 204 |

`RoutineView` is `{ routineId, name, notes, movements, createdAt, lastSession }`, where `lastSession`
is the most recent **completed** session trained from the routine, as the session envelope with its
`$type: "completed"`, or `null`.

- **Refusals.** A name that is empty once trimmed, no movements, a movement named twice, or a
  `workingSets` outside 1–20 is a 400 naming the field (`Name`, `Movements`,
  `Movements[i].WorkingSets`). A movement that is not the athlete's is a 404.
- **From a session.** The session must be the athlete's (404) and completed (409). Its movements
  are read in first-appearance order, `workingSets` being the count of that movement's sets that are
  neither warm-ups nor drop sets (a drop set extends the set before it); a movement trained only in warm-ups is left out, and a session left with no
  movement is a 400 on `SessionId`. An ad hoc session takes the new routine as its origin in the same
  save, so the routine answers with it as `lastSession`; a session already trained from another
  routine keeps that origin.

## Bodyweight — `/api/bodyweight`

One measurement per calendar day: logging a day again replaces it.

| Verb | Route | Body / query | Answers |
|---|---|---|---|
| PUT | `/{day}` (yyyy-MM-dd) | `{ weight, unit }` | 200 `BodyweightView` |
| GET | `?from=&to=` | `DayRange` | `[BodyweightView]`, day ascending |
| GET | `/latest` | — | the newest `BodyweightView`, or 404 when none is logged |
| DELETE | `/{day}` | — | 204, or 404 when that day has none |

`BodyweightView` is `{ day, weight, unit }`. A weight that is not more than 0 and at most 1000 is a
400 on `Weight`. Bodyweight on a day with no measurement of its own is the newest measurement on or
before it; that rule is the reader's, so no route answers it.

## Body-composition scans — `/api/body-composition/scans`

A scan is one analyser result sheet (InBody), entered from a reviewed form.

| Verb | Route | Body | Answers |
|---|---|---|---|
| POST | `/` | `ScanRequest` | 201 `ScanView`; 409 when a scan at that day and minute is recorded |
| GET | `/` | — | `[ScanView]`, oldest first, unpaged |
| PUT | `/{id}` | `ScanRequest` | 200 `ScanView`, replacing the sheet whole; 404, or 409 onto another scan's minute |
| DELETE | `/{id}` | — | 204, or 404 |

`ScanRequest` is `{ day, time, sheet, recordsBodyweight }`: `day` and `time` (`HH:mm:ss`) are the
sheet's own printed clock. `ScanView` is `{ id, day, time, sheet }`. A sheet is the core every
analyser prints — `device` (the model as printed, e.g. `InBody570`), `unit` (the `WeightUnit` its
masses are in), `height: { value, unit }` (`LengthUnit`: 0 inches, 1 centimetres), `weight`,
`skeletalMuscleMass`, `bodyFatMass`, `leanBodyMass`, `percentBodyFat`, `bodyMassIndex` — and
`sections`, the panels this analyser printed, each at most once, discriminated by `$type`:
`bodyWater { intracellular, extracellular, total }`, `dryLeanMass { mass }`,
`segmentalLean { segments }` and `segmentalFat { segments }` (segments are `rightArm`, `leftArm`,
`trunk`, `rightLeg`, `leftLeg`, each `{ mass, percentOfReference }` — the analyser's own reference),
`extracellularWaterRatio { value }`, `fitnessScore { points }`, `massControl { bodyFat, leanBody }`
(signed), `basalMetabolicRate { kilocalories }`, `visceralFatLevel { level }`, and
`impedance { rows: [{ kilohertz, ohms: segments of numbers }] }`. A panel not printed, or not
legible, is absent. Every implausible figure is a 400 naming its field (`Sheet.Weight`,
`Sheet.SegmentalLean.LeftLeg.Mass`), all at once.

`recordsBodyweight: true` makes the sheet's weight the bodyweight for `day`, replacing one already
logged there, in the same save; `false` leaves the day as it is. Deleting a scan leaves that
bodyweight logged.

## Movement setup — `PUT /api/movement-datas/{id}/setup`

`{ notes, addsToBodyweight }` → 200 `MovementDataDTO`, 404 when the movement is not the athlete's.
Notes are trimmed and run to at most 2000 characters (400 on `Notes`). Every `MovementDataDTO` carries
`setup: { notes, addsToBodyweight }`; `addsToBodyweight` says the weight logged on the movement is
plate added to the athlete's own bodyweight, so the load moved is bodyweight plus that.

## Contexts — `/api/contexts`

Two creates rather than one polymorphic body — flat request shapes generate a far better TypeScript
client, and it matches the purpose-built-request rule.

`POST /exertion` (`Period`, `Notes`, `Effort`) · `POST /state` (`Period`, `Notes`) ·
`GET /{id}` · `GET ?from=&to=` · `PATCH /{id}/notes` · `DELETE /{id}`

## Training programs — `/api/training/programs`

`POST /` → `PlannedTrainingProgram` (`Title`, `StartDate`, `EndDate`, `Tags[]`) ·
`POST /{id}/activate` · `POST /{id}/complete` (`Summary`) ·
`GET /{id}` · `GET /` · `PATCH /{id}` (title/dates/tags) · `DELETE /{id}`

## Focus — `/api/training/focus`

A **focus pin** names one of the athlete's movements to emphasise over a horizon, at a number of
sessions a week, perhaps toward a target. Lifecycle: `pinned → unpinned`, unpinning dated by the
client. An unpinned pin is kept as history; delete is for a pin made by mistake.

| Verb | Route | Body | Answers |
|---|---|---|---|
| GET | `/` | — | every pin, `FocusPinView[]`: pinned first, then by `starts`, latest first |
| POST | `/` | `MovementDataId`, `Starts`, `Ends`, `SessionsPerWeek`, `Target?`, `Priority?` (`push` \| `pull` \| `legs`) | 201 with the view; 404 if the movement is not the athlete's |
| PATCH | `/{id}` | `Starts`, `Ends`, `SessionsPerWeek`, `Target?` — the terms, replaced whole | 200 with the view; 409 if unpinned |
| POST | `/{id}/unpin` | `On` (a date) | 200 with the view; 409 if already unpinned |
| DELETE | `/{id}` | — | 204 |

`Target` is `{ weight, unit, reps }` or absent — a nullable object for the reason program
membership crosses as a nullable id. The view sends it back as the domain's two cases, and the
status likewise:

```
FocusPinView {
  focusPinId, movementDataId,
  role: { "$type": "emphasis" } | { "$type": "priority", family: "push" | "pull" | "legs" },
  starts: "yyyy-MM-dd", ends: "yyyy-MM-dd",
  sessionsPerWeek,
  target: { "$type": "none" } | { "$type": "lift", weight, unit, reps },
  status: { "$type": "pinned" } | { "$type": "unpinned", on: "yyyy-MM-dd" },
  pinnedAt
}
```

`Ends` before `Starts`, `SessionsPerWeek` outside 1–7, a negative or non-finite target weight, an
unknown unit, or target reps outside 1–30 are each a 400 naming the field (`Ends`,
`SessionsPerWeek`, `Target.Weight`, `Target.Unit`, `Target.Reps`). The movement is fixed for a
pin's life — a different movement is a different focus.

**Readings, not states.** Whether a horizon has begun or passed, and whether the target was met,
are read by the client against today and the training log, the way a planned session is read as
missed. None of them is stored, so none of them can disagree with the log.

**Priorities.** A pin with `Priority` is that family's priority lift for its horizon; without one it
is for emphasis. Pinning a priority lets the family's pinned priority go in the same commit, unpinned
the day before the new horizon starts (or on its own first day, if the new one starts earlier). A
movement whose classification trains another family is a 400 on `Priority`, and so is `core`; an
unclassified movement is taken at the athlete's word.

### Priorities — `GET /api/training/priorities/{day}`

`PriorityReport { window, families: [FamilyPriority], cardio: CardioMeasure }` for the seven days
ending on `{day}`. Each `FamilyPriority { family, lift }` has `lift` `$type` `none` (nothing pinned and
no compound lift of the family in eight weeks) or `chosen`:
`{ movementDataId, name, choice, progress, sessionsLastFourWeeks, series }`. `choice` is `pinned`
(carrying the `FocusPinView` in force on the day) or `mostTrained` (`sessions` in eight weeks).
`progress` is `notTrained` | `noEstimate` (trained, no set of 1–12 reps at a load) | `firstBack`
(`best`, nothing in the twelve weeks before) | `compared` (`best`, `priorBest` of the twelve weeks
before, `allTimeBest` before the window, `change`, and `standing`: `lifetimeBest`, `newBest`, `held`
within 5 % of the prior best, or `below`), each with `unit` and `onSystemLoad`. Estimates are Epley on the load moved: bodyweight plus plate for a movement whose
setup adds to bodyweight, with bodyweight on a day the newest on or before it and the first weighing
standing in before any. `series` is `SessionBest { day, estimatedOneRepMax, load, reps }` for each
session of the last twelve weeks and the window.

## Movement patterns and volume

### Classification — `/api/movement-bases/{id}/classification`

| Verb | Body | Answers |
|---|---|---|
| PUT | `{ pattern, primary: [muscle], secondary: [muscle] }` | 200 `MovementBaseDTO` — the athlete's statement replaces the map's entry |
| DELETE | — | 200 `MovementBaseDTO` — the statement withdrawn, so the map answers again |

Patterns travel as `horizontalPush`, `verticalPush`, `horizontalPull`, `verticalPull`, `squat`,
`hinge`, `singleLeg`, `isolation`, `other`; muscle groups as `chest`, `frontDelts`, `sideDelts`,
`rearDelts`, `triceps`, `lats`, `upperBack`, `biceps`, `forearms`, `quads`, `hamstrings`, `glutes`,
`adductors`, `calves`, `lowerBack`, `abs`. No primary group, or a group both primary and secondary, is
a 400 naming `Primary` or `Secondary`; another athlete's base is a 404. Every `MovementBaseDTO` (and
so every `MovementDataDTO`'s `movementBase`) carries `classification`: `$type` `unclassified` or
`classified` `{ classification: { pattern, primary, secondary }, source, family }`, `source` being
`athlete`, `map`, or `mapUnsure` with its `reason`, and `family` the one it counts toward. The map is
`Services/Training/Patterns/movement-patterns.json`.

`GET /api/muscle-groups/get-all` is gone, and the movement-base create and update requests no longer
carry `MuscleGroups`: what a movement trains is its classification. The web app's muscle-group picker
still calls the old route.

### Weekly volume — `GET /api/training/volume/{day}`

`TrainingVolume` for the seven days ending on `{day}`: `families` (push, pull, legs:
`{ family, hardSets, target: 8, lastTrained? }`), `patterns` (all nine: `{ pattern, hardSets, target?,
lastTrained? }`, `target` 4 for the seven compound patterns and absent for isolation and other),
`muscles` (all sixteen: `{ muscle, direct, indirect, sets, target: 4, lastTrained? }`, `sets` being
`direct + indirect / 2`), `unplaced` (`{ movementDataId, name, hardSets }` for movements nothing
classifies) and `upNext`. A hard set is a working, drop or failure lift; `lastTrained` looks back
over the whole log. `upNext` is `$type` `nothingLogged` or `train`
`{ families, lastTrained?, trainedToday, readiness }`, `families` the one to three trained longest
ago, together when last trained the same day; `readiness` is `notRead`, `usual { score }` or
`low { ouraScore, usualLow, usualHigh }` — Oura's readiness score (Oura's 0–100 rating, not a
measurement) under the tenth percentile of its last 90 days. A day trains a family with any compound
set of it, or when its accessory work is at least half the day's hard sets, so face pulls finishing a
push day do not make it a pull day.

## Training score — `/api/training/score`

| Verb | Route | Answers |
|---|---|---|
| GET | `/{day}` | `TrainingScore` for the seven days ending on `{day}` |
| GET | `/weeks?from=&to=` | `WeeklyScore[]`, one per Sunday in the range, oldest first; more than 52 Sundays is a 400 on `To` |

`TrainingScore { window, value, outOf, unknown: [part], lifting, cardio, progress, consistency }`. Each part
carries `points` and `outOf` and the measurements it was read from:

Each measurement pays `sqrt(min(1, measured / target))` of its points: concave, because the first
half of a target buys most of what the whole would, and only the whole target pays in full.

- `lifting` (35): `families` as in weekly volume; each family pays its share of a third.
- `cardio` (25): `points` null when unknown; `measure` `$type` `measured { zone2Minutes, zone2Target:
  150, highIntensityMinutes, highIntensityTarget: 20, sessions, heartRateDays }` or `unknown { reason:
  noHeartRate | noMaximumHeartRate | sessionsUnrecorded }`. Zone 2 pays 15, zones 4–5 pay 10, each
  capped at its target, from the cardio report's sessions.
- `progress` (20): `families` as in the priority report; each family pays a third — a new or
  lifetime best whole, `held` or `firstBack` a quarter, `below` or `noEstimate` a tenth, `notTrained`
  or `none` nothing. With every other part full, holding all three priorities reads 85: 90 takes a
  new best.
- `consistency` (20): `sessionsPerBlock`, completed sessions in the window and the three seven-day
  blocks before it, each paying its share of a quarter against `target` 3.

`value` is the points the known parts earned; `outOf` is 100 less the weight of any part in
`unknown`. A part with no data is named rather than measured as zero, and its points are out of
reach, so a week with the ring off never outscores the same week with it worn and no cardio.
`WeeklyScore { window, value, outOf, unknown, liftingPoints, cardioPoints?, progressPoints,
consistencyPoints }`. Chat reads the score through the `GetTrainingScore` tool as a
`TrainingScoreDigest`: a few lines of the same measurements, what is up next, what is under target,
and up to twelve weeks — a few hundred tokens.

## Oura — `/api/oura`

Read-shaped: the native client is the consumer, so each route answers with the stored documents
rather than anything computed. Design and the collections behind it: `docs/oura-integration.md`.

| Verb | Route | Body / query | Answers |
|---|---|---|---|
| GET | `/days?from=&to=` | `DayRange` | one `OuraDayView` per day any collection covered |
| GET | `/days/{day}` | — | that day's `OuraDayView`, or 404 |
| GET | `/days/latest` | — | the newest day any collection covers, as an `OuraDayView`, or 404 when nothing is stored |
| GET | `/sleep?from=&to=` | `TimeWindow` | sleep periods, with stages, hypnogram and both overnight curves |
| GET | `/workouts?from=&to=` | `TimeWindow` | workouts the ring saw or the athlete logged on it |
| GET | `/sessions?from=&to=` | `TimeWindow` | guided meditation, breathing and rest |
| GET | `/tags?from=&to=` | `TimeWindow` | tags the athlete marked |
| GET | `/heartrate?from=&to=` | `TimeWindow` | every all-day heart-rate sample in the window, oldest first: `{at, bpm, source}`. At most 31 days; longer is 400 |
| POST | `/sync` | `DayRange` | `OuraSyncReport` — per-collection tallies, including what Oura sent that could not be read |

**Two window types, deliberately.** The daily documents are keyed by the calendar day Oura files
them under, so `/days` takes a `DayRange` of `DateOnly`; asking for them in instants would make the
caller invent a time zone for midnight. Events occupy clock time and are windowed on the stamped
`StartTime` column, so they take the same `TimeWindow` contexts and sessions do.

**Absence is absence, not zero.** A collection Oura filed nothing for on a given day is null on the
day view, and a day with nothing at all is missing from a range rather than present and empty.

`/sync` answers 200 with its tallies rather than 204: it is the one write here whose outcome the
caller cannot see any other way. A failure to reach Oura or to authenticate is `Problem.Upstream`,
so 502; a missing stored credential is a `Conflict`, so 409. **Oura rejecting the stored token is
also a 502**, with a detail naming the token — a 401 from this route means the caller's own
Lionheart session, never Oura's credential.

`/days/latest` is what a client syncs from: the newest stored day says how far behind the store is,
which a range read could only answer by guessing how far back to look. The newest day takes the
latest of all eight daily collections, since they cover different days.

A day view carries `vo2Max` and `cardiovascularAge` beside the original six, each null where Oura
issued nothing, and `activity.timeline` — the day's five-minute activity classification as Oura's
digits with the instant its first slot starts — null on days stored without one.

## Cardio — `/api/cardio`

| Verb | Route | Body / query | Answers |
|---|---|---|---|
| GET | `/?from=&to=` | `DayRange`, at most 367 days | `CardioReport` |

Derived on the server from the logged sessions, the ring's workouts, the all-day heart-rate series
and Oura's fitness estimates; design in `docs/oura-integration.md` → "The cardio and habits
reports". The report carries `maxHeartRate` (`$type` `agePredicted` | `observed`, or null with no
usable age), `sessions` (each with a `source` — `$type` `ringWorkout` | `loggedSession` — and a
`heartRate` — `$type` `recorded` | `notRecorded`), one `weeks` entry per Monday-to-Sunday week the
range touches, empty weeks included and each with `daysInRange` so a part-week at either end reads
as short, the `vo2Max` (Oura's), `appleWatchVo2Max` (the Apple Watch's, from Apple Health — its
own series, never merged with Oura's), `cardiovascularAge` and `restingHeartRate` series, `aerobicBase` (the last
four weeks against the eight before, `appleWatchVo2Max` beside `vo2Max`, each figure null where its window has no data — zone 2 also
when there is no maximum heart rate, or when less than half the window's session time has heart
rate synced), and `excluded` (ring workouts left out, counted by category and `reason`:
`sameAsLoggedRun`, `insideLoggedLifting`, `shortWalk`, `strength`, `notTraining`, `unclassified`,
`noDuration`). A `loggedSession` source carries `isRecorded` — imported from Apple Health, so
`GET /api/training/sessions/{id}/recording` has its route — and `activity` for an endurance session
(`ride`, `indoorRide`, `hike`, `walk`, `indoorRun`; null for a trail run or a distance-and-time
session, which `isTrailRun` tells apart); a `recorded` heart rate carries
`source`: `ring` (the all-day series over the session's clock time) or `appleHealth` (the recording
app's own samples, read ahead of the ring's for an imported run).

## Apple Health — `/api/health`

| Verb | Route | Body | Answers |
|---|---|---|---|
| POST | `/workouts` | `HealthWorkoutImport` | `HealthImportOutcome`: 201 `imported` (with `session`), 200 `alreadyImported` (`trainingSessionId`) or `notImported` (`reason`, and `trainingSessionId` when it matched a logged session) |
| POST | `/measurements` | `HealthMeasuresImport` | `HealthMeasuresImportReport` |
| GET | `/measurements?from=&to=` | `DayRange` | `HealthMeasureSeries[]` |
| POST | `/bodyweights` | `HealthBodyweightsImport` | `HealthBodyweightImportReport` |
| PUT | `/birth-date` | `HealthBirthDate` | `ProfileAgeView` |

The phone offers one Health workout at a time, as Health holds it: `workoutId` (its Health UUID),
`activity` (Health's spelling: `running`, `cycling`, `hiking`, `walking`), `indoor`, `appName` and
`appBundleId` (the app that wrote it), `start` and `end` at the offset the workout was recorded in,
`duration` (the app's, a `TimeSpan`), `distanceMeters` and `elevationAscendedMeters` (null when not
stated), `route` (every fix: `at`, `latitude`, `longitude`, `altitude` or null), `heartRate` (`at`,
`bpm` — the same app's samples) and `metrics` (optional: one entry per metric, `metric` and
`samples` of `at`/`value`; `metric` is `power` W, `cadence` rpm, `speed` m/s, `groundContactTime`
ms, `verticalOscillation` cm, `strideLength` m or `heartRateRecovery` bpm; an unknown metric or an
impossible reading is dropped, not refused). Which workouts become sessions is the server's rule
(`docs/apple-health.md`), so every answer but a refusal is a 2xx; `reason` is `readDirectly`,
`otherActivity`, `noDistance`, `sameAsLoggedRun` (inside any logged cardio session),
`insideLoggedLifting` or `deleted` (its session was imported and then deleted). Idempotent on
(athlete, `workoutId`): `alreadyImported` writes nothing, except `metrics` its recording lacked,
filled in once. A workout that cannot be read is a 400 (a heart-rate reading that is not one is
dropped, not refused); a 409 is a workout older than a standing record it contests while a record
is withdrawn, since placing it would replay the records. The same workout imported by two requests
at once answers `alreadyImported` to the second.

`GET /api/training/sessions/{id}/recording` answers `RecordedWorkoutView` for a session imported
from Health, and 404 for any other: `activity` (`run`, `ride`, `indoorRide`, `hike`, `walk`,
`indoorRun`), `recordedBy`, `start`, `end`, the session's `distanceMeters`,
`duration` and `climbMeters`; `movingTime`, `descentMeters` from the route (null without one);
`pace` and `movingPace` per kilometre; `heartRate` (the cardio report's `SessionHeartRate`, its
`source` included), `maxHeartRate`, and `trace` (`source`, `samples` of `at`/`bpm`) for drawing;
`kilometres` and `miles` (each split: `number`, `distanceMeters`, `elapsed`, `moving`, `pace` per
its own unit, `climbMeters`, `descentMeters`, `averageBpm`); `route` (`latitude`, `longitude`); and
`metrics` (each: `metric`, `average` over its samples, `peak`, `samples` of `at`/`value`; empty when
the app wrote none — heart-rate recovery is one sample). A ride's speed is the client's conversion
of `pace`. The route goes only to the athlete's own client — the chat model's `GetRecordedWorkouts`
has none.

**Measurements.** The phone offers days of what Apple's own devices measured: `days`, each
`metric`, `device` (`appleWatch` | `iPhone`), `day`, and that day's `mean`, `minimum`, `maximum`
and `count` of readings, in the metric's fixed unit — `vo2Max` (ml/kg/min), `restingHeartRate` and
`walkingHeartRateAverage` (bpm), `heartRateVariabilitySdnn` (ms; SDNN, never compared with Oura's
RMSSD), `respiratoryRate` (breaths/min), `walkingSpeed` (m/s), `walkingStepLength` (m),
`walkingDoubleSupport`, `walkingAsymmetry` and `walkingSteadiness` (fractions, 0 to 1), and
`sixMinuteWalkDistance` (m). A day already held for that metric and device is restated. The answer
counts `recorded`, `restated`, `unchanged` and `unreadable` (a day whose numbers are outside the
metric's bounds or out of order, left out while the rest of the batch is kept). The read answers one
series per metric and device: `metric`, `device`, `days` (each `day`, `mean`, `minimum`, `maximum`,
`count`), oldest first.

**Weigh-ins.** `weighIns`, each `day` (on the phone's clock), `at` and `kilograms`. Each day's first
weigh-in becomes that day's bodyweight measurement, in the unit of the athlete's newest measurement
(else the unit most of his lifts are logged in, else kilograms), to a tenth; a day already measured
is kept as it was. The answer counts `added`, `daysAlreadyMeasured`, `unreadable`, and
`setsReweighed`: sets on a movement whose weight adds to bodyweight whose system load now reads a
different bodyweight. Records are not replayed — they are on the logged weight, and system load is
read fresh on every read.

**Date of birth.** `dateOfBirth`; the profile's age becomes the age on the server's today and the
answer is `age` and `changed`. A date in the future or more than 120 years back is a 400.

## Habits — `/api/habits`

| Verb | Route | Body / query | Answers |
|---|---|---|---|
| GET | `/?from=&to=` | `DayRange`, at most 367 days | `HabitsReport`: one `HabitsDay` per stored activity day, and `current` and `previous` summaries — the range, and the equal span before it — each split into weekdays, weekends and all days |
| GET | `/days/{day}` | — | that day's `HabitsDayDetail` — its classified runs, sittings, sleep and training on the clock — or 404 when no activity is stored for it |

Activity classes travel as `nonWear`, `rest`, `inactive`, `low`, `medium`, `high`, `unrecognised`.
Clock figures in a sleep timing are minutes: a bedtime from noon, a wake time and midpoint from
midnight, so bedtimes either side of midnight average correctly.

## Chat thread and memory — `/api/chat/thread`, `/api/chat/memories`

One continuing chat per athlete, beside the standalone conversations under `/api/chat/conversation*`
(which predate this spec and keep their verb-carrying routes). The model never sees the whole thread,
only the **session** a message lands in: a session ends when the thread sits idle for three hours or
its stored transcript passes about 60k tokens, and the next message opens a new one. An ended
session is distilled into **memory** before the next opens, so the new session starts with what was
said in the old one rather than its transcript.

| Verb | Route | Body / query | Answers |
|---|---|---|---|
| GET | `/api/chat/thread` | `before?` — an instant, the `openedAt` of the oldest session already shown | `ChatThreadPage`: up to ten `sessions`, oldest first, and `hasEarlier` |
| POST | `/api/chat/thread/messages` | `{ content }` | the `ThreadSessionView` the exchange went into, whole — a new session when the message opened one. 400 for an empty message, 502 when the model failed; a failed exchange stores nothing |
| GET | `/api/chat/memories` | — | every current `MemoryView`: goals, injuries and preferences first, then newest first |
| PUT | `/api/chat/memories/{id}` | `{ kind, text }` | the new `MemoryView`; the version it replaces is kept and listed under `earlier`. 409 when the memory is no longer current |
| DELETE | `/api/chat/memories/{id}` | — | 204; the memory and every earlier version of it are gone |

- **`ThreadSessionView`** — `sessionId`, `openedAt`, `messages[]`: the athlete's and the model's turns
  in order, each the same `LHChatMessageDTO` a conversation carries (`role` 0 athlete, 1 model). Tool
  results are not turns and are not sent.
- **`MemoryView`** — `memoryId`, `kind` (int: `0` goal, `1` injury, `2` preference, `3` commitment,
  `4` decision, `5` fact), `text`, `recordedAt`, `source` (int: `0` distilled from the chat, `1`
  written by the athlete), `quotes[]` (`messageId`, `byAthlete`, `said` — clipped to 280 characters,
  `at`), and `earlier[]` (`kind`, `text`, `recordedAt`, `replacedAt`), newest first.
- A memory changes by being **superseded**: a correction, or a session that revises it, writes a new
  current version and keeps the old one dated. A session can also **retire** a memory that no longer
  holds; a retired memory is no longer listed.
- Thread sessions are stored as conversations but are not standalone ones: the conversation routes
  neither list them nor accept their ids.
- The chat model reaches memory through the `SearchMemory` tool, offered to standalone
  conversations too.

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
