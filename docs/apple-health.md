# Apple Health

What Lionheart reads from Apple Health, which of it becomes training, and how a workout that
several sources saw is counted once.

Where the code lives: the phone's HealthKit layer in `ios/Lionheart/Health/`, its sync in
`ios/Lionheart/Features/HealthSync/`, a recorded workout's page in
`ios/Lionheart/Features/RecordedWorkout/`; on the server, the domain in `Model/Health/`,
`Model/Training/Sets/` (the endurance set) and `Model/Training/Sessions/SessionSource.cs`, the rules
and services in `Services/Health/Import/` and `Services/Health/Recordings/`, the shared same-session
rule in `Services/Cardio/SessionMatching.cs`, the EF mapping in `Data/HealthConfiguration.cs`, routes
in `Endpoints/Health/` and `Endpoints/Training/Sessions/GetRecordedWorkoutEndpoint.cs`, tests in
`lionheart.Tests/Health/`.

## Why Health

The athlete records his runs in Strava. Oura's API never exposes the workouts Oura imported from
Strava, so before this his runs reached Lionheart only as whatever the ring auto-detected — no
distance, route, splits or pace. Strava's own API is ruled out: its 2026 terms forbid feeding its data
to an AI, which Lionheart's chat does. Strava can write each run to Apple Health with its route
("Send to Health"), and HealthKit is available on the free developer tier, so the phone reads it
there.

**Read only.** Lionheart asks to read workouts, workout routes, heart rate, a ride's power, cadence
and speed, a run's power, speed and form, and the heart rate's one-minute recovery, and never asks to
write. The one exception is the "Write sample workouts to Health" button in Settings, which seeds
invented workouts into a simulator's empty Health store and exists only in a Debug build for the
simulator — a Debug build installed on the phone (Xcode's Run) does not have it, so the athlete's
own Health store can never be given an invented workout.

## A workout becomes a completed session

An imported workout is a `CompletedTrainingSession` holding one set, rather than a record of its
own. That one decision is what makes it first-class with nothing else built: the personal records
detect on it, History's log and day pages list it, the cardio report counts it, the training score's
cardio reads it through the cardio report, and the chat model's session counts include it.

- **An outdoor run** is a `TrailRunSet` — distance in kilometres, the recording app's duration, its
  climb — competing on trail running. Trail running is the record subject because the set names no
  movement and a route's distance is whatever the route was; the subject's own reasoning
  (`RecordSubject`) holds for a road run as much as for a trail one.
- **Anything else taken** is an `EnduranceSet`: the same measures and an `EnduranceActivity` —
  `ride`, `indoorRide`, `hike`, `walk` or `indoorRun`. Its own shape rather than a trail run carrying
  an activity, because a ride is not a run. Indoor is part of the activity rather than a flag,
  because it changes what the session is evidence of.

**Records.** A ride competes on **riding** and a hike on **hiking**, subjects of their own beside
trail running, for trail running's reason: the set names no movement, and a season of rides ranks
against itself, not against the runs. Neither keys on terrain or bike. Three kinds compete on
nothing:

| Activity | Records | Why |
|---|---|---|
| Outdoor run | trail running | |
| Ride | riding | |
| Hike | hiking | |
| Indoor ride (a trainer, Peloton) | none | Its distance is the machine's estimate from power or a flywheel, not ground covered, so it would stand beside the riding records as if measured. |
| Indoor run (a treadmill) | none | The same rule as the indoor ride: the watch estimates a treadmill's distance from the wrist, and a belt's is the machine's. It is training — history, cardio, the score — and nothing else. |
| Walk, indoors or out | none | Movement rather than an effort to rank: a walk to the shop would set a "record". It is history, and cardio when it is as long as a ring walk must be to count (30 minutes); shorter, the cardio report leaves it out as `shortWalk`, as it does the ring's. |

The subjects that name no movement are stored in the records' movement-id column as sentinels —
`Guid.Empty` for trail running, the next two ids up for riding and hiking — so the domain never sees
a nullable id; the wire sends `movementDataId` absent and a `subject` kind beside it.

Where it came from is a domain fact on the session, `SessionSource`: `AthleteLog` (entered in
Lionheart, or imported from his own logs — Strong, the coached sheets) or `AppleHealth` (the
workout's Health UUID and the app that recorded it). It is orthogonal to `Provenance`, which says who
authored the content: an imported workout is the athlete's own training, so it is `Provenance.User`.

What the session does not hold — the route, the app's heart rate and its other metrics — sits beside
it in `WorkoutRecordings`, keyed by the session's id. Separate because a route is thousands of fixes
that no read of the log should carry, and because keeping the athlete's location out of the sessions
table means nothing that reads sessions can hand it on by accident. Stored column by column (a start
instant and parallel arrays) rather than an object per fix: about 150 KB for an hour at one fix a
second, against 350. Each metric is stored the same way.

A correction to an imported session restates its set like any other session's — the phone states an
endurance set back whole — and the route stays as recorded. Deleting the session deletes its
recording, and remembers the workout as deleted (`DeletedHealthWorkouts`), so no later read of
Health imports it again.

## Which workouts count

One pure rule, `HealthWorkoutDecisions.Decide`, run on the server for every workout the phone
offers. The phone reads every app's runs, rides, hikes and walks and decides nothing, so the rule is
written and tested once. In order:

| Health workout | Becomes | Why |
|---|---|---|
| Written by Oura (`com.ouraring.*`) | left out: `readDirectly` | Oura writes its workouts to Health — most of the walks and hikes there, and some rides — and Lionheart already holds them from the Oura API, where the cardio report reads them and the ring's walk rules apply. |
| Written by Strong (`io.strongapp.*`) | left out: `readDirectly` | Strong's workouts arrive through its export import. |
| Its session was imported and then deleted | left out: `deleted` | The athlete's deletion stands. |
| Not a run, ride, hike or walk | left out: `otherActivity` | Elliptical, stairs and the rest: a handful each, with no distance that means anything. The ring's own workout still carries any of them into the cardio report. The phone queries the four activities only, so this is a guard. |
| A workout with no distance and no route | left out: `noDistance` | Nothing to measure it by. |
| A workout lying half inside a session already logged | left out: `sameAsLoggedRun` / `insideLoggedLifting` | The same workout, already counted — see below. |
| An outdoor run | a trail run | |
| A ride, a hike, a walk, an indoor ride or run | an endurance set of that activity | See the records table above. |

The bundle prefixes are the apps' own identifiers as Health reports them; a Strava, Runna, Garmin
Connect, Peloton or Apple Watch workout is taken. Bundle identifiers rather than names, since a name
is localised. The "eligibility" of a workout is this one function: no screen, reader or report
repeats any part of it.

## Counted once

**The rule is the cardio report's, shared.** A workout lying at least half its own length inside a
logged session is that session (`SessionMatching`, extracted from the cardio report for this second
caller). A logged cardio session — a run, a ride, a hike, a walk — is matched ahead of a lifting
session. The answer's wire name stays `sameAsLoggedRun` for any of them. The cardio report applies it to the
ring's workouts on every read; the import applies it once, before a Health workout becomes a session.

That covers every overlap the athlete's sources produce:

- **Strava and the ring.** The ring auto-detects most runs, and Oura's API serves that detection.
  Once the Strava run is a session, the ring's workout inside it is excluded from the cardio report as
  `sameAsLoggedRun`, exactly as it would be for a run logged by hand.
- **Oura's copy in Health.** Left out by source, above.
- **A run he also logged in Lionheart.** The Health run lies inside the logged one, so it is not
  imported, and the answer names the session it already is.
- **Two apps writing the same workout** (Strava and a watch both writing one ride). The first
  offered is imported; the second lies inside it and is left out. The phone offers in Health's
  insertion order, oldest first within a page.
- **A watch's walk and another app's hike or ride.** A watch saves the walk it detected the moment
  it ends; Strava writes the hike when it uploads, later — and a backfill replays Health in that
  order. So a walk is the weakest reading of a stretch of time: a run, ride or hike lying inside
  nothing but an imported walk replaces the walk (its session, recording and projection go, and it
  is not remembered as deleted; a walk sets no record, so nothing else moves). A walk arriving after
  the hike is the hike. A walk the athlete logged himself is never replaced.
- **Inside a logged lifting session.** Left out, as the cardio report leaves out a ring workout there:
  the gym as another device saw it. A deliberate run recorded in the middle of a logged gym session
  would also be left out by this; if that turns out to happen, it is a change to the shared rule.

A logged session added after a Health run it overlaps is not reconciled: both stand. The rule runs at
import, and the athlete logs his runs in Strava rather than by hand.

## Idempotent, and resumable

The session's source carries the Health UUID; a stamped column and a unique index on
(athlete, UUID) make a second session for one workout unrepresentable. Offering a workout again
answers `alreadyImported` and writes nothing.

The phone keeps HealthKit's own anchor (`HKQueryAnchor`, archived) and asks Health only for what was
added after it, ten workouts a page. The anchor moves on only once every workout on a page has been answered,
so a sync that stops — the server out of reach — starts the next one at that page, and the server's
idempotence absorbs the overlap. A first sync starts with no anchor, which is the whole of Health's
history. Signing out forgets the anchor: the next account's server holds none of these workouts.

**Reading more.** An anchor marks a place in one query's results, so the kept anchor carries the
*reading* it belongs to (`WorkoutReader.reading`). When the activities read change — runs alone
became runs, rides, hikes and walks — an anchor of another reading is no anchor, and the next sync
reads Health's whole history once more. That backfill is idempotent with what the first import
stored: a workout already imported answers `alreadyImported` and writes nothing, except that its
recording takes the metrics it now carries where it held none — which is how a run imported before
running form was read gets it. A workout deleted from the log since deletions were remembered
answers `deleted`; **one deleted before then comes back** on that first re-read — nothing recorded
its deletion — and is deleted again by hand, after which it stays deleted. A first backfill
offers every Oura-written walk too, a thousand small answers of `readDirectly`.

A workout Health deletes later is not deleted from Lionheart; the session can be deleted by hand,
and once deleted it stays deleted through any later read of Health, a fresh install's included.

**Deploying.** The server goes first. A phone that reads rides, hikes and walks answering to a server
that does not take them would be told each is not a run — an answer, so the anchor would pass
them and they would not be offered again until the reading moved on once more.

**Records and history arriving out of order.** A backfilled workout older than a standing record it
contests cannot be judged against what stands — it would claim to have beaten a record set
after it. Such a workout is placed by replaying the athlete's records over his whole history in the same
transaction, the rule a correction to a session already follows. While any record is withdrawn the
replay would award it again, so that workout is refused with a sentence saying so. The phone counts it
as not taken yet, holds its id back, and offers it again first on every sync after, so it lands once
the condition clears rather than being passed by the anchor and forgotten. A heart-rate reading that
cannot be one (zero, or past 260 bpm) is dropped from the workout rather than refusing it.

## When it syncs

On returning to the app (at most once per 15 minutes, and only once Health is connected), and on pull
to refresh in History and on the Cardio screen. The "Apple Health" row — beside Oura's on Today, and
in Settings — says when the last sync finished, how many workouts it imported, and what it left out and
why. Connecting is the athlete's tap on that row: the app never springs Health's permission sheet on
him at launch. Health never tells an app whether reading was refused; a refusal reads as no workouts.

## Metrics beyond heart rate

What else the recording app wrote across the workout, by the same same-source rule as heart rate:
a ride's power (W), cadence (rpm) and speed (m/s) — a trainer's every second, a power meter's — and a
run's power, speed, ground contact time (ms), vertical oscillation (cm) and stride length (m), which
an Apple Watch has written since 2022. The heart rate's one-minute recovery is the one sample a watch
writes just after the workout ends; the phone looks for it in the quarter hour after. Each is a
series held in the recording (`RecordedMetrics`); a reading past what any rider or runner produces
is dropped as an impossible heart rate is, and a metric a newer phone names is passed over rather
than refusing the workout. Where two apps wrote one run, only the watch's copy carries its form; if
the other copy was imported first, the watch's copy is left out as the same run and its metrics are
filled into that session's recording, where it held none.

The workout's page leads a ride with its average and peak power and its cadence, gives a run a
**Running form** section of averages, draws each series against its mean, and puts the recovery
among the figures. The chat model gets the averages and the recovery as one line.

## Heart rate

The recording app's own samples over the workout, where it wrote any: heart-rate samples in the
workout's interval **from the same source as the workout**. Same source, because Oura writes the
ring's heart rate to Health too, and that series is already Lionheart's from Oura directly.

Otherwise the ring's all-day series over the same clock time, from Lionheart's own Oura store. The
workout's page, the cardio report and the chat model's summary all say whose it is
(`HeartRateSource`: `ring` or `appleHealth`). Nothing is invented: with neither, the workout has no heart
rate.

The cardio report reads an imported workout's heart rate the same way, so the workout's page and the
cardio screen never give one workout two averages. The zones are drawn from the cardio report's maximum as it
stands on the workout's own day; the cardio screen draws them from the maximum at the end of its span,
so where a later session has raised the observed peak, the two can split a workout's minutes between
zones a little differently. The maximum's observed peak may now be read from a strap's samples as
well as the ring's, which is the better evidence of the two.

## What the route gives

Derived on the server on every read (`RouteAnalysis`, pure and tested), so a better derivation needs
no migration:

- **Distance** along the route by haversine. The workout's distance is still the app's stated one; the
  route is scaled to it, so the splits add up to the distance the workout is recorded as.
- **Moving time**: fix-to-fix legs faster than 0.5 m/s. A steep power-hike counts as moving; a phone
  standing still, or a gap the app left while paused, does not.
- **Climb and descent**: altitude changes counted once they pass a 3 m dead band, so a phone's
  altitude jitter on a flat road is not a hill. The workout's recorded climb is the app's stated ascent
  where it wrote one, else the route's.
- **Splits** per kilometre and per mile: elapsed and moving time, moving pace per whole unit (a short
  last split reads at its own pace), climb, descent and average heart rate. A remainder under 10 m is
  not a split.

## Location privacy

A route says where the athlete lives. It is stored in his own database, sent only to his own phone
(and remembered there for offline reading like any read, forgotten at sign-out), and drawn only on
the workout's page. The chat model gets summaries — distance, time, pace or speed, splits, heart
rate, climb, the app's averages — through `GetRecordedWorkouts` and one line on the context card, and is told no route is
available. `WorkoutFacts` is the only text the model receives about a recorded workout and never
writes a coordinate; a test holds it to that. No committed fixture holds a real coordinate, id, date or value: test routes are
invented, run due north from an empty meadow.

## Setting it up on the phone

1. In Strava, turn on sending activities to Apple Health (in recent versions: Settings →
   Applications, Services and Devices → Health). Strava writes each run from then on, with its route.
2. Install this build. HealthKit needs the capability on the signing team, which the free tier
   allows; Xcode adds it from the entitlement when it signs.
3. In Lionheart: Today's or Settings' "Apple Health" row → Connect → allow everything in Health's
   sheet (Workouts, Workout Routes, Heart Rate, and the cycling, running and recovery types). Recent iOS then asks how much history to share: choose **All
   Recorded Data and Future Data**, or the first sync can only backfill the last 30 days. The first
   sync reads everything Health lets it.

## Measurements: the watch, the phone's gait, weigh-ins, date of birth

A second reader, `MeasurementReader`, takes what Lionheart otherwise lacks: the Apple Watch's
VO2max, resting heart rate, HRV, respiratory rate and walking heart rate (Oura issues no VO2max, and
its data starts years after the watch's); the iPhone's walking speed, step length, double support,
asymmetry, Walking Steadiness and six-minute walk estimate; every weigh-in; and the date of birth.
Its types join `HealthLibrary.readTypes`, so an athlete already connected for runs sees the row ask
to connect again and one sheet lists the new types. Its sync, `HealthMeasureSyncStore`, runs beside
the workout sync — on returning to the app (at most hourly), from its own row in Body's gait section
— and keeps its own mark. Headphone and environmental audio, dietary data, rings, mindful minutes
and Garmin's history are not read.

**Only Apple's own measurements.** A reading counts when Health's own device source wrote it
(`com.apple.health.<device>`) and the hardware was a watch or an iPhone (`MeasuringDevice.measuring`,
on the phone). Oura's app writes its overnight RMSSD under Health's SDNN type, from the iPhone it
runs on; the bundle check is what keeps it out. Apple's HRV is SDNN and Oura's is RMSSD — different
statistics over different windows — so the watch's figures are stored apart from Oura's
(`HealthDailyMeasures`, keyed by athlete, figure, device and day), labelled by device on every
screen, and never drawn on one axis with Oura's.

**Days, not readings.** The phone reduces a window's readings to one row per figure, device and
calendar day — mean, lowest, highest, count — because a walking speed is read per walk and six
years of them are hundreds of thousands of samples. A day measured by the watch and the phone keeps
both rows. Every sync reads again from a week before the last day it finished: the watch hands over
a night late and Health issues estimates after the fact, and the server restates a day that grew. A
first sync reads from Health's oldest reading, a quarter at a time, the mark kept after each. The weigh-ins of the whole span go first, in one request, so the count of re-read sets below is
counted once rather than once per quarter.

**Weigh-ins fill the gaps.** Each day's first weigh-in becomes that day's bodyweight measurement
when the day has none; a day logged in Lionheart, or measured by a scan, is kept. Health holds mass
in no unit, so the server states it in the unit of the athlete's newest measurement — else the one
most of his lifts are logged in — because a bodyweight adds to a set's plate only in the set's unit.
Personal records are on the logged weight and nothing stored holds a system load, so there is no
replay: the import reports how many weighted dips and pull-ups now read a different bodyweight, and
the priority report and the score read the new system load on their next read.

**Date of birth** sets the profile's age when it is unset or a birthday has passed; it is the age
the cardio zones' predicted maximum falls back on.

**Where it shows.** The gait figures lead Body's gait section, each a four-week mean against the
twelve weeks before, opening onto its history, with an info sheet on each. Asymmetry and double
support — the two that rise when a leg is favoured — sit on an injury's page, eight weeks before
against four from it. The watch's history has its own screen, linked from Body and from the
HRV, resting-rate and breathing trends Today opens. The cardio report carries the watch's VO2max as
`appleWatchVo2Max`, its own series and comparison beside Oura's.
