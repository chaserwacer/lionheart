# Oura integration

What Lionheart stores from the Oura ring, what it deliberately does not, and how the athlete's
existing history got into the database.

Where the code lives: domain in `Model/Oura/`, wire records in `Model/Oura/Wire/`, mapping and
services in `Services/Oura/`, the archive importer in `Services/Oura/Import/`, EF mapping in
`Data/OuraConfiguration.cs`, routes in `Endpoints/Oura/`, tests in `lionheart.Tests/Oura/`.

## Why this shape

The point of the app is that a language model reasons over the whole picture, so the instruction
was to pull everything Oura allows. "Everything" still needs judgement: a collection that is
Oura's opinion rather than a measurement adds noise, and a time series stored per night is a real
storage and query decision rather than a free one. The two sections below say what was kept and
what was not, and why.

Two facts about the data shaped the model more than anything else.

**Oura's collections are two different shapes.** Six describe a calendar day — one document per
day. Four describe events inside a day — zero to many each, occupying clock time. Modelling the
second kind as more columns on a day row would be wrong: a day with thirteen workouts on it is not
a day with thirteen workout columns.

**Oura's own document id is a UUID, unique per collection and stable across pulls.** That is the
primary key of every table here, and it is what makes both the importer and the live sync
idempotent with nothing else to arrange: a document either exists in the table or it does not.
The integration this replaced instead stored a sync date per day and compared it against the day it
described in order to guess which days were still provisional.

## What is stored

One table per collection. Each row is `OuraId` (Oura's own id, the primary key), `UserId`, `Day`
(the day Oura files the document under), and the measurement itself as a JSON column. Event
collections also carry an indexed `StartTime`, stamped from the reading's `Period` on every save
the same way the training domain's is. The all-day heart-rate series is the one exception, and has
its own section below: its samples carry no id, so its rows are keyed by athlete and UTC day.

| Collection | Table | Shape | Why it is worth storing |
|---|---|---|---|
| `daily_activity` | `OuraDailyActivity` | one per day | Steps, calories, MET minutes and the time spent in each activity band — the day's training load outside anything logged. |
| `daily_readiness` | `OuraDailyReadiness` | one per day | Oura's recovery headline and its nine contributors. The most directly useful number in the whole surface. |
| `daily_sleep` | `OuraDailySleep` | one per day | The night's score and its seven contributors. |
| `daily_spo2` | `OuraDailySpo2` | one per day* | Overnight blood oxygen and a breathing-disturbance index — two independently absent numbers, and disturbed breathing bears directly on recovery. |
| `daily_stress` | `OuraDailyStress` | one per day | Seconds of the day read as stressed and as recovered, plus Oura's one-word verdict. Load and recovery, measured. |
| `daily_resilience` | `OuraDailyResilience` | one per day | Oura's resilience level and its three recovery components. No archived history — see below. |
| `vO2_max` | `OuraVo2Max` | zero or one per day* | Oura's VO2max estimate (ml/kg/min) and when it made it. An estimate from heart rate and walking, not a gas-exchange test; its value is its trend. No archived history. |
| `daily_cardiovascular_age` | `OuraDailyCardiovascularAge` | one per day | Oura's predicted vascular age and the pulse-wave velocity it derives from it. A model output, kept for its trend. No archived history. |
| `heartrate` | `OuraHeartRateDays` | one run per UTC day | The all-day heart-rate series, ~288 samples a day at rest and denser in workouts, each with Oura's `source` (awake, rest, sleep, workout, session, live). What the cardio report reads heart-rate zones from. No archived history. |
| `sleep` | `OuraSleepPeriods` | zero to many per day | The richest recovery source there is: per-period stages, efficiency, latency, and the overnight heart-rate and HRV curves the daily score flattens away. |
| `workout` | `OuraWorkouts` | zero to many per day | Training load the ring saw and Lionheart did not record itself. |
| `session` | `OuraSessions` | zero to many per day | Guided meditation, breathing and rest, with their own sample runs. Three records so far; the table is nearly free and rejecting it would mean adding one back the first time he meditates again. |
| `tag` | `OuraTags` | zero to many per day | The closest thing in the Oura surface to the athlete's own account of a day — sauna, alcohol, travel, a late coffee. A bad night explains itself when the same night carries `tag_sleep_alcohol` and `tag_sleep_late_hotbath`. |

\* A day is deliberately **not** unique for SpO2, nor for VO2max, which Oura's schema does not
promise one of per day. Oura issued two documents a day for 128
consecutive days (2024-11-01 to 2025-11-03), identical in content for all but three of them. Both
are stored, because Oura's id is the key and storing what it actually sent is more honest than
picking one at the boundary. A day read takes one of them in document-id order, so the same call
does not answer differently twice.

### Time series: what is kept and what is not

| Series | Size | Decision |
|---|---|---|
| `sleep.heart_rate`, `sleep.hrv` | ~94 samples per night at 5 minutes; ~102k slots across the archive, ~1.5 MB of JSON | **Kept**, as JSON on the sleep-period row. The overnight HRV curve is the highest-value recovery signal in the data, and 1.5 MB total is nothing. Stored as one run per night rather than one row per sample: ~190 rows a night for data only ever read whole is a quarter of a million rows bought for nothing. |
| `sleep.sleep_phase_5_min` | ~94 characters per night | **Kept**, verbatim as Oura's digit-per-slot encoding, and decoded where it is drawn. The hypnogram is the shape of the night. Oura documents the digits (OpenAPI 1.43, `sleep_phase_5_min`): 1 deep, 2 light, 3 REM, 4 awake. Checked against the athlete's 1,053 archived main sleeps: of every assignment of the four digits to the four stages, that one is the closest to Oura's own stage durations — a mean of 23 minutes off per night across all four stages, against 68 for the next best — so the decoder follows the documentation and the archive agrees. Storage stays verbatim so a decoder could be corrected without a migration. |
| `sleep.sleep_phase_30_sec`, `sleep.movement_30_sec` | ~938 characters per night each; ~2 MB, ~19% of the sleep payload | **Rejected.** Ten times the bytes of the 5-minute hypnogram for a resolution nothing in the app reads, and the two strings' lengths disagree with each other in 799 of 1425 archived records, so they cannot be aligned sample-for-sample anyway. |
| `sleep.app_sleep_phase_5_min` | ~94 characters per night | **Rejected.** A second hypnogram from a different algorithm. It disagrees with the first in 1359 of 1425 archived records — most of them in ten or more slots, not at the margins — so keeping both would leave two real and different answers to "when was he in deep sleep". |
| `daily_activity.class_5_min` | 288 characters per day; ~0.4 MB across the archive | **Kept**, verbatim with the instant its first slot starts — Oura's activity day runs from 04:00 local, and the document's `timestamp` carries that offset. **Reversed 2026-11-03:** it was first rejected because no consumer wanted an intraday profile, and the athlete now does — sittings at the desk and how long they run, when in the day he moves and trains, weekdays against weekends. Those are shapes inside a day, which the daily totals sum away. Oura's digits (OpenAPI 1.43): 0 non-wear, 1 rest, 2 inactive, 3 low, 4 medium, 5 high; counting each digit's slots lands within minutes of Oura's own per-minute time totals on the same day. The totals stay the figures of record, since Oura computes them per minute. |
| `daily_activity.met` | exactly 1440 samples per day; 1.78M samples, **87% of that file's 10.4 MB** | **Still rejected.** Everything the habits report reads comes from Oura's own five-minute classification; reading sittings from minute-resolution MET instead would mean choosing a MET cut-off for "sitting" that Oura has already chosen once. With no reader, it stays out. The daily MET-minute totals Oura sends beside it are kept. |
| `session.*` three runs | 39–99 samples at 5 seconds, 3 records | **Kept.** Same envelope as sleep's, and trivially small. |

### Fields dropped from collections that are otherwise stored

- **`sleep.readiness`** — a full readiness snapshot nested inside each sleep record, agreeing with
  that day's own `daily_readiness` document on 1027 of 1053 main sleeps. Keeping both would leave
  two answers to "how recovered was he". Oura's own `readiness_score_delta` and `sleep_score_delta`
  are kept, since they are deltas rather than duplicates.
- **`sleep.ring_id`** (null in every archived record) and **`sleep.low_battery_alert`** (false in
  every one) — device bookkeeping with no analytic content.

## What is not stored, and why

**`sleep_time`** — Oura's recommended bedtime window. Rejected because it is Oura's coaching
advice rather than a measurement: it says what Oura thinks the athlete should do, not what his body
did. `optimal_bedtime` is null in 96.7% of the archived records and the recommendation is
`earlier_bedtime` in 678 of 1025, so there is little signal even taken at face value. The archive
file is present and the importer names it in its report rather than skipping it silently.

**Collections Oura exposes that are not built at all:** `personal_info`, `ring_configuration`,
`rest_mode_period`, `enhanced_tag`. The first two are device and profile metadata rather than
anything to reason over; the last two have no reader.

`vO2_max`, `daily_cardiovascular_age` and `heartrate` were held back here at first, for want of a
recorded payload to pin their shapes. **Built 2026-11-03**, because the athlete's goals are trail
running and better cardio, and these three are the only cardio Oura measures or estimates. They are
mapped from Oura's published schema (OpenAPI 1.43) rather than from a recorded payload, which is
the one thing to check after the first live sync: every unreadable document is counted and
described in the tally, so a schema the API does not actually honour shows up as rejections rather
than as silently missing data.

## The collections with no archived history

`daily_resilience`, `vO2_max` and `daily_cardiovascular_age` are stored and the live sync pulls
them, but the athlete's archive has no file for any of them, so their history begins with the
first sync that reaches back. The importer reports each as a distinct outcome (`NoArchiveFile`)
rather than as zero records, because "the ring recorded none" and "there is no file" are different
facts. They are also the readings whose shapes no recorded payload pins.

`heartrate` has no archive shape at all: its samples carry no document id for the importer to key
on, so the live sync is its only way in. A sync over a long range is how its history is backfilled
— see below.

## The all-day heart-rate series

Oura serves `heartrate` by instants (`start_datetime`/`end_datetime`) rather than days, as bare
`{timestamp, bpm, source}` samples in UTC. Three decisions follow.

- **One run per UTC day, keyed by athlete and day.** The same reasoning as the overnight curves —
  several hundred rows a day for data only ever read as a span is rows bought for nothing — and
  UTC because the samples come with no local offset, so a UTC day is the one boundary drawable
  without guessing where the athlete was. Readers never see the day: they ask for a window of
  instants (`/api/oura/heartrate`, or a session's own clock time inside the cardio report).
- **Pulled in whole UTC days, and each day replaced wholesale.** A sync for days `From`–`To` asks
  for `From` 00:00Z to the midnight two days after `To` — the athlete's evening is already the next
  UTC day — in slices of at most 30 days. Oura's OpenAPI description (1.43) states no maximum
  window for this route; client libraries report refusals past about a month, and 30 days stays
  under that. Because every slice starts and ends at a UTC midnight, each day it returns is
  complete as of the pull, so replacing the stored day is idempotent the way an Oura id is for the
  other tables. Each slice is saved as it arrives, so a long backfill that is rate-limited partway
  keeps what it finished.
- **Samples are not evenly spaced.** Nominally five minutes, faster during a workout, absent while
  the ring is off. Each keeps its own instant, and the cardio report credits a sample with the time
  to the next one, capped at five minutes, so a gap reads as time without a reading rather than as
  time at the last one.

A sample that cannot be read is counted and described per sample in the tally, like a document.

**Size.** At the nominal rate a day is about 17 KB of JSON (288 samples at ~60 bytes), more on
workout days: roughly 6–8 MB a year, 20–25 MB for the three years Oura holds. The database copy
this was measured against is 95 MB.

## Quirks in the real data, preserved rather than smoothed over

These are all visible in the archive and all deliberately stored as they arrived.

- **`sleep.day` is the waking day, not the date the night began on.** They disagree on 343 of 1425
  records. Both are kept: `Day` is what a reader asking "how did he sleep on the 8th" means, and
  the `Period` is where the night sits on the clock.
- **`average_heart_rate` is `0`, not null, on 148 records** — nearly all brief naps the ring took no
  reading across. Stored as Oura sent it. Rewriting a zero to absence on the way in would destroy
  the only evidence of which of the two Oura actually said; the matching `heart_rate` run is
  all-null on exactly those records, which is how a reader tells.
- **Samples inside a run are nullable, and about one overnight slot in seven is empty.** A gap is
  real data. Collapsing it to zero would read as a heart rate of zero; dropping it would shift
  every later sample earlier in the night.
- **Nulls are shaped, but not always paired.** All six activity contributors do go null together
  with the score, on 15 days. SpO2's two readings do not: 25 archived records carry one without
  the other. And `spo2_percentage` is nulled as a whole object rather than having a null average,
  so a mapping that reached for its `average` unguarded would throw on one night in ten.
- **A missing reading is absent, never a sentinel.** The integration this replaced substituted `0`
  for a null contributor and `-1` for a day with no document, which made a day Oura scored badly
  indistinguishable from a day it did not score, and dragged every baseline average down in
  proportion to the days the ring was off the finger.
- **Oura's vocabularies are stored as the strings Oura sent** — a workout activity, a stress day
  summary, a resilience level, a sleep kind. They are an external vocabulary Oura extends on its
  own schedule (22 workout activities appear in the archive out of a longer list), nothing in
  Lionheart branches on them yet, and a closed C# set here would mean a record the importer has to
  reject the day Oura adds a value. The first analytic that branches on one is the moment to give
  that vocabulary a typed set.

## The archive import

Three and a half years of Oura history already sat on disk from an earlier project, pulled from
2023-01-01 onward. Re-pulling it would be slow, rate-limited and would need a credential;
importing gives the same result immediately and gives the iOS client a populated database to
develop against.

```bash
dotnet run -- import-oura <archive-directory> --user <athlete-guid>
```

A command rather than a development-only endpoint: it takes a filesystem path, which is an
argument rather than something a request body should carry; it needs no identity story, since the
athlete is named on the command line; and it adds no write route that would have to be kept out of
production. It builds a host with configuration and the database only — the web host refuses to
start without LM Studio configured, which an import has no use for.

The files are real but old, so they are treated as untrusted input. Every line is parsed and mapped
on its own. A line that cannot be read becomes a rejection naming the file, the line number and
the reason, and the run carries on; nothing is dropped silently and nothing aborts the import.
Running it twice stores nothing the second time.

**The run against the athlete's archive: 9194 documents stored, 0 rejected.**

| Collection | Stored | Days covered |
|---|---|---|
| `daily_activity` | 1239 | 2023-01-05 → 2026-08-01 |
| `daily_readiness` | 1058 | 2023-01-06 → 2026-08-01 |
| `daily_sleep` | 1058 | 2023-01-06 → 2026-08-01 |
| `daily_spo2` | 1258 | 2023-01-06 → 2026-08-01 |
| `daily_stress` | 1060 | 2023-09-08 → 2026-08-02 |
| `daily_resilience` | — | no archive file |
| `sleep` | 1425 | 2023-01-06 → 2026-08-01 |
| `workout` | 2004 | 2023-08-07 → 2026-08-01 |
| `session` | 3 | 2024-07-18 → 2024-07-19 |
| `tag` | 89 | 2023-08-31 → 2026-07-01 |

Only Oura data is imported. The same archive holds historical *training* data, which the owner's
decision of 2026-07-19 abandoned with no backfill (`docs/data-remodel.md`); the importer does not
read it and has no switch that would.

**Backfilling the five-minute classification.** Days imported before `class_5_min` was kept hold
no timeline. Running the same import again fills it in: each document is keyed by Oura's id, so
the archived payload is applied over the stored one and the change tracker writes only the days
that differ. Days after the archive's last day (2026-08-01) get theirs from a sync. Because the
import also re-applies the archived version of the days the live sync has since covered
(2026-07-25 onward), run a sync over those days afterwards so they carry Oura's latest.

## The live sync

`POST /api/oura/sync` pulls every collection above for a range of days and upserts what comes
back, answering with per-collection tallies. A document Oura sends that this application cannot
read is counted and described rather than failing the run — one malformed record must not cost a
month of good days. Failing to reach Oura, to authenticate, or to read a page at all is a
`Problem.Upstream` and does end the run, because nothing after it would mean anything.

**The live pull has run against the real API.** On 2026-11-03, the owner's first sync covered
2026-07-25 to 2026-11-03 with the stored personal access token. It stored 1,184 documents across
every collection and rejected none, so the token still works and paging returns more than one page.
The tests cover everything short of the network: the paging, the failure mapping, and, through the
mapping both paths share, what a real Oura response turns into.

**Backfilling from the command line.** `dotnet run -- sync-oura --user <athlete-guid> --from
<yyyy-MM-dd> --to <yyyy-MM-dd>` runs the same pull with the stored credential, for ranges the
app never asks for. It needs neither a sign-in nor the token in hand, and pulls a calendar month
at a time, so a refusal partway leaves every earlier month stored. The API serves history from
before the archive import began, heart rate included.

Things a reader should know before trusting the pull:

- **Paging was broken in the integration this replaces.** It declared Oura's cursor as `NextToken`
  with no JSON name, so it never bound to the `next_token` Oura sends and every collection stopped
  after one page. Nothing surfaced; the sync simply stored less than Oura held. There is a test on
  the cursor now.
- **Oura retired personal access tokens at the end of 2025** (reported by its own OpenAPI
  description; not verified against a live call here). The stored credential is still a personal
  access token, so the first live sync may answer 401. The client's message for 401 says so
  explicitly rather than leaving a stale credential looking like an outage. As of 2026-11-03 it
  still works.

  **Decided 2026-11-03 (owner): stay on the personal access token until it actually stops
  working.** The retirement is documentation rather than an observed failure, the token may well
  keep working for an existing integration, and OAuth2 would mean a redirect flow, a client
  secret and a token store for one user who already has a working credential. The trigger for
  revisiting is a real 401 from a real sync, which the client already names. Do not migrate
  pre-emptively.
- **The activity collection needs an end date a day past the last day wanted**, or it omits that
  last day. Found by hand by the earlier integration and preserved; it is applied only to that one
  collection so the workaround stays attached to the quirk it documents.
- **VO2max, cardiovascular age and heart rate have not run live.** They were added after the first
  live sync. Their mapping follows Oura's published schema; the first sync that includes them is
  the check, and its `rejected` count per collection is the number to read. They run last, and a
  refusal of a whole route from any of the three — a scope the credential lacks, a page whose
  types do not match the schema — is reported as that collection's single rejection ("the
  collection could not be read: …") rather than ending the run.

## Should Oura days feed `AnalyzableEntry`?

**No** — the recommendation, with the reasoning, since this was an open question.

`AnalyzableEntry` is the flat read model every retrieval query goes through, and it is written by
*decoration*: facets, a valence, a summary line written to be embedded, and the vector of that
line. It exists to make things the athlete **said** semantically retrievable.

An Oura day is forty numbers. Four reasons not to project it:

1. **Embedding it produces a vector worth nothing.** "Readiness 62, sleep score 71, HRV 48"
   embeds to a point whose nearest neighbours are other number-soups, and it would dilute the
   semantic index the training entries depend on.
2. **A time join is strictly better for what it is actually wanted for.** The hidden-cost analytic
   wants sessions against the next day's recovery. Oura rows are keyed by day with an indexed
   `Day` and `StartTime`; joining them directly is exact, cheap and needs no model. Going through
   a facet filter to reach the same rows is worse in every respect.
3. **There is no mechanism to maintain it.** Decoration costs a model call per entry — about 1240
   of them for this history — and `InterpretationCommitter` is create-only, so nothing could
   re-decorate them when `DecoratorVersion` moves (`docs/data-remodel.md` next-steps #2).
4. **It would change what the analysis model means.** `EntryKind` is `Session | Context` — things
   that happened and were described. Adding machine telemetry makes "every analyzable entry" a
   different and vaguer set.

What to do instead, when the time comes: analytics join the Oura tables on day or `StartTime`
directly. Where an Oura fact deserves to be *narratively* retrievable, the existing `Context` type
already fits — an Oura tag is a `StateContext` in all but name, and tags carry the athlete's own
words. That is a worthwhile future step and is not built here. The one thing that would change this
answer is retrieval needing "find me nights like this one" by similarity, and the answer to that
is a separate vector index over sleep metrics, not this one.

## Reading the data back

Routes are read-shaped and simple, because a native client is the consumer. See
`docs/api-surface.md` for the route table.

The chat model reaches the same data through two tools, `GetOuraDays` and `GetOuraSleepNights`.
The sleep tool omits the overnight sample runs: a night carries about 96 heart-rate and 96 HRV
samples, which the native client charts and a model can do nothing with but spend its context on.
The days tool omits the five-minute activity classification for the same reason. The HTTP routes
hand both over in full.

## The cardio and habits reports

Two derived reads built on the server, because their input is large and their answer small; each
is also a chat tool (`GetCardioReport`, `GetHabitsReport`). Code in `Services/Cardio/` and
`Services/Habits/`, read models in `Model/Cardio/` and `Model/Habits/`, routes in
`docs/api-surface.md`.

**Cardio** (`GET /api/cardio`). The sessions are the logged Lionheart sessions carrying trail-run or
distance sets, plus the ring's workouts once their noise is taken out: housework, yard work and
stretching; walks under 30 minutes (the median auto-detected walk is 19); strength work; and any
ring workout lying at least half inside a logged session, which is that session as the ring saw it.
What was excluded is counted by reason in the report. A run imported from Apple Health is a logged
run here, so the ring's workout inside it is excluded the same way (`docs/apple-health.md`). Each
session's heart rate is read from the all-day series over its own clock time — except an imported
run's, read from the recording app's own samples where it wrote any — and says whose it is. Zones
are percentages of maximum heart rate (50/60/70/80/90), with maximum the higher of the Tanaka age
prediction (208 − 0.7 × age, from the profile) and the 99th percentile of the cardio sessions'
samples over the last year (the ring's, and an imported run's own): a peak above the prediction is
evidence, one below it is as likely a finger sensor under-reading a hard effort.
Weekly load is Edwards' TRIMP (zone minutes × zone number). "Is the aerobic base moving" is four
weeks against the eight before: zone-2 minutes per week, cardio minutes per week, VO2max, the
nights' lowest heart rate and cardiovascular age.

**Habits** (`GET /api/habits`, `GET /api/habits/days/{day}`). Read from the five-minute
classification, the sleep periods and the training log. A sitting is a waking run of inactive slots
and the brief rest inside them, broken by one slot of movement, by the ring coming off, by sleep,
and by half an hour or more of rest outside a scored sleep — which in this archive is unscored
naps, unscored nights, and whole days of the ring lying on a table with zero steps. A day counts
toward the summaries when its classification covers at least 23 hours — so today, still in
progress, never does — and no more than three hours of it are unaccounted for that way. Every
summary is weekdays, weekends and all days, for the range and for the equal span before it, so each
figure is read against his own previous span rather than a norm.

## Tests

`lionheart.Tests/Oura/`. The fixtures under `Oura/Archive/` are **invented payloads in the exact
shape Oura sends** — its field order, nulls, zeros, offsets and quirks, with made-up ids, days and
readings — kept as files rather than string literals so the importer can be pointed at a directory
the way it is in production. `Oura/Damaged/`
holds records broken on purpose — a bad id, a day that is not a date, truncated JSON, a reversed
sleep period, a missing sample run — for what the importer does with input it cannot read.
