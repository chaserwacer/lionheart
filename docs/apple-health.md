# Apple Health

What Lionheart reads from Apple Health, which of it becomes training, and how a run that several
sources saw is counted once.

Where the code lives: the phone's HealthKit layer in `ios/Lionheart/Health/`, its sync in
`ios/Lionheart/Features/HealthSync/`, the run's page in `ios/Lionheart/Features/Run/`; on the server,
the domain in `Model/Health/` and `Model/Training/Sessions/SessionSource.cs`, the rules and services
in `Services/Health/`, the shared same-session rule in `Services/Cardio/SessionMatching.cs`, the EF
mapping in `Data/HealthConfiguration.cs`, routes in `Endpoints/Health/` and
`Endpoints/Training/Sessions/GetRecordedRunEndpoint.cs`, tests in `lionheart.Tests/Health/`.

## Why Health

The athlete records his runs in Strava. Oura's API never exposes the workouts Oura imported from
Strava, so before this his runs reached Lionheart only as whatever the ring auto-detected — no
distance, route, splits or pace. Strava's own API is ruled out: its 2026 terms forbid feeding its data
to an AI, which Lionheart's chat does. Strava can write each run to Apple Health with its route
("Send to Health"), and HealthKit is available on the free developer tier, so the phone reads it
there.

**Read only.** Lionheart asks to read workouts, workout routes and heart rate, and never asks to
write. The one exception is a Debug build's "Write sample runs to Health" button in Settings, which
seeds invented runs into a simulator's empty Health store and is compiled out of Release.

## A run becomes a completed session

An imported run is a `CompletedTrainingSession` holding one `TrailRunSet` — distance in kilometres,
the recording app's duration, and its climb — rather than a record of its own. That one decision is
what makes it first-class with nothing else built: the personal records (longest distance, fastest
time at a distance, best pace) detect on it, History's log and day pages list it, the cardio report
counts it as a logged run, the training score's cardio reads it through the cardio report, and the
chat model's session counts include it.

Trail running is the record subject because the set names no movement and a route's distance is
whatever the route was; the subject's own reasoning (`RecordSubject`) holds for a road run as much
as for a trail one, and the athlete's goal is trail running.

Where it came from is a domain fact on the session, `SessionSource`: `AthleteLog` (entered in
Lionheart, or imported from his own logs — Strong, the coached sheets) or `AppleHealth` (the
workout's Health UUID and the app that recorded it). It is orthogonal to `Provenance`, which says who
authored the content: an imported run is the athlete's own training, so it is `Provenance.User`.

What the session does not hold — the route and the app's heart rate — sits beside it in
`WorkoutRecordings`, keyed by the session's id. Separate because a route is thousands of fixes that
no read of the log should carry, and because keeping the athlete's location out of the sessions
table means nothing that reads sessions can hand it on by accident. Stored column by column (a start
instant and parallel arrays) rather than an object per fix: about 150 KB for an hour at one fix a
second, against 350.

A correction to an imported session restates its set like any other session's; the route stays as
recorded. Deleting the session deletes its recording.

## Which workouts count

One pure rule, `HealthWorkoutDecisions.Decide`, run on the server for every workout the phone
offers. The phone reads every app's runs and decides nothing, so the rule is written and tested once.

| Health workout | Becomes | Why |
|---|---|---|
| Written by Oura (`com.ouraring.*`) | left out: `readDirectly` | Oura writes its workouts to Health; Lionheart already holds them from the Oura API, and the cardio report reads them there. |
| Written by Strong (`io.strongapp.*`) | left out: `readDirectly` | Strong's workouts arrive through its export import. |
| Not a run | left out: `notARun` | Only runs are taken so far. Walks, hikes and rides would each need a movement to hang their records off, which is a catalogue decision for the athlete. The phone queries runs only, so this is a guard. |
| A treadmill run (indoor) | left out: `indoor` | No route, and a distance the app estimated, which would stand beside the trail-running records as if measured. The ring's own workout still carries it into the cardio report. |
| A run with no distance and no route | left out: `noDistance` | Nothing to measure it by. |
| A run lying half inside a session already logged | left out: `sameAsLoggedRun` / `insideLoggedLifting` | The same run, already counted — see below. |
| Any other outdoor run | a completed session | |

The two bundle prefixes are the apps' own identifiers as Health reports them; a Strava, Runna,
Garmin Connect or Apple Watch run is taken. Bundle identifiers rather than names, since a name is
localised.

## Counted once

**The rule is the cardio report's, shared.** A workout lying at least half its own length inside a
logged session is that session (`SessionMatching`, extracted from the cardio report for this second
caller). A logged run is matched ahead of a lifting session. The cardio report applies it to the
ring's workouts on every read; the import applies it once, before a Health workout becomes a session.

That covers every overlap the athlete's sources produce:

- **Strava and the ring.** The ring auto-detects most runs, and Oura's API serves that detection.
  Once the Strava run is a session, the ring's workout inside it is excluded from the cardio report as
  `sameAsLoggedRun`, exactly as it would be for a run logged by hand.
- **Oura's copy in Health.** Left out by source, above.
- **A run he also logged in Lionheart.** The Health run lies inside the logged one, so it is not
  imported, and the answer names the session it already is.
- **Two apps writing the same run** (Strava and a watch's own app). The first offered is imported;
  the second lies inside it and is left out. The phone offers in Health's insertion order, oldest
  first within a page.
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
added after it, ten runs a page. The anchor moves on only once every run on a page has been answered,
so a sync that stops — the server out of reach — starts the next one at that page, and the server's
idempotence absorbs the overlap. A first sync starts with no anchor, which is the whole of Health's
history of runs. Signing out forgets the anchor: the next account's server holds none of these runs.

A workout Health deletes later is not deleted from Lionheart; the session can be deleted by hand.
Deleting an imported session and signing in again on a fresh install would import it again.

**Records and history arriving out of order.** A backfilled run older than a standing trail-running
record it contests cannot be judged against what stands — it would claim to have beaten a record set
after it. Such a run is placed by replaying the athlete's records over his whole history in the same
transaction, the rule a correction to a session already follows. While any record is withdrawn the
replay would award it again, so that run is refused with a sentence saying so, the sync counts it as
not taken, and moves on.

## When it syncs

On returning to the app (at most once per 15 minutes, and only once Health is connected), and on pull
to refresh in History and on the Cardio screen. The "Apple Health" row — beside Oura's on Today, and
in Settings — says when the last sync finished, how many runs it imported, and what it left out and
why. Connecting is the athlete's tap on that row: the app never springs Health's permission sheet on
him at launch. Health never tells an app whether reading was refused; a refusal reads as no runs.

## Heart rate

The recording app's own samples over the workout, where it wrote any: heart-rate samples in the
workout's interval **from the same source as the workout**. Same source, because Oura writes the
ring's heart rate to Health too, and that series is already Lionheart's from Oura directly.

Otherwise the ring's all-day series over the same clock time, from Lionheart's own Oura store. The
run's page, the cardio report and the chat model's summary all say whose it is
(`HeartRateSource`: `ring` or `appleHealth`). Nothing is invented: with neither, the run has no heart
rate.

The cardio report reads an imported run's heart rate the same way, so the run's page and the cardio
screen never give one run two averages; the zones and their maximum come from the cardio report in
both places. The maximum's observed peak may now be read from a strap's samples as well as the
ring's, which is the better evidence of the two.

## What the route gives

Derived on the server on every read (`RouteAnalysis`, pure and tested), so a better derivation needs
no migration:

- **Distance** along the route by haversine. The run's distance is still the app's stated one; the
  route is scaled to it, so the splits add up to the distance the run is recorded as.
- **Moving time**: fix-to-fix legs faster than 0.5 m/s. A steep power-hike counts as moving; a phone
  standing still, or a gap the app left while paused, does not.
- **Climb and descent**: altitude changes counted once they pass a 3 m dead band, so a phone's
  altitude jitter on a flat road is not a hill. The run's recorded climb is the app's stated ascent
  where it wrote one, else the route's.
- **Splits** per kilometre and per mile: elapsed and moving time, moving pace per whole unit (a short
  last split reads at its own pace), climb, descent and average heart rate. A remainder under 10 m is
  not a split.

## Location privacy

A route says where the athlete lives. It is stored in his own database, sent only to his own phone
(and remembered there for offline reading like any read, forgotten at sign-out), and drawn only on
the run's page. The chat model gets summaries — distance, time, pace, splits, heart rate, climb —
through `GetRecordedRuns` and one line on the context card, and is told no route is available.
`RunFacts` is the only text the model receives about a run and never writes a coordinate; a test
holds it to that. No committed fixture holds a real coordinate, id, date or value: test routes are
invented, run due north from an empty meadow.

## Setting it up on the phone

1. In Strava, turn on sending activities to Apple Health (in recent versions: Settings →
   Applications, Services and Devices → Health). Strava writes each run from then on, with its route.
2. Install this build. HealthKit needs the capability on the signing team, which the free tier
   allows; Xcode adds it from the entitlement when it signs.
3. In Lionheart: Today's or Settings' "Apple Health" row → Connect → allow Workouts, Workout Routes
   and Heart Rate in Health's sheet. The first sync reads Health's whole history of runs.

## Next

A second reader — the other data Oura and the phone write to Health that Lionheart lacks — joins
`HealthLibrary`: its types added to `readTypes`, so the athlete is asked once for everything, and a
reader of its own beside `WorkoutReader`.
