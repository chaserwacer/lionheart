# iOS architecture

Why each piece of the native client has the shape it does. It carries only what a reader cannot get
from the code: the reasoning, the server behaviours the client is built around, and the traps
already paid for. `.claude/skills/lionheart-ios` owns *where things go*; this file owns *why*.

Read alongside `docs/api-surface.md` (the route-level spec) and `docs/data-remodel.md` §"Domain
model" (the sealed hierarchies the wire types mirror).

## The shape in one paragraph

`Core/` is pure values and functions and imports no UI framework, so every decision a screen makes
is unit-tested without a host — `ios/Scripts/check-layering.sh` is what keeps that true. `Net/` is
one client and a set of endpoint types that hold routes and nothing else. `Auth/` owns the tokens
and is the only thing that renews them. `Features/<Area>/` holds screens and their stores, where a
store does IO and turns each outcome into one state case. `Store/ActiveWorkout/` is the single
exception to "the server is the truth": the in-flight workout is the device's, because the gym is
where the signal is worst.

## Decoding: total, lenient, and why

The server's responses are camelCase, its enums cross the wire as **integers**, and its sealed
hierarchies carry a `$type` discriminator. Three consequences shape every type in `Core/Model/`.

**A `$type` the client has not heard of is a thing that will happen.** The backend gains a case; a
deployed app meets it. So every hierarchy has an `.unrecognised` case and decodes through a total
initialiser. A session in a lifecycle state this build does not know still carries its envelope, so
it still has an id, a date and notes, and still appears in a list.

**A row that cannot be read must not empty a screen.** `lenientList` (inside an object) and
`LenientList` (a top-level array) drop an element that fails and keep the rest. They are only
reached by genuinely malformed payloads, since an unfamiliar discriminator is already a case. The
mechanism is worth knowing: a throwing `decode` leaves an unkeyed container's cursor where it was,
so skipping an element needs a second decode into a type whose initialiser cannot fail.

**There is exactly one row the client refuses.** A training session with no id or no date has
nothing to show and nothing to act on, so it throws — and `LenientList` drops it. Everything else
degrades.

Two scalar spellings are not what the OpenAPI document says, because the document describes the C#
types and not the converters that actually run:

- **`Rpe` is a bare number or `null`**, not `{isRated, value}`. `RpeJsonConverter` writes it that
  way. Verified against the running server.
- **`TimeSpan` is `[-][d.]hh:mm:ss[.fffffff]`**, parsed into seconds. The optional day part is
  separated by the same `.` the fractional seconds use, so it is only a day count when it appears
  before the first colon.

Instants arrive in three spellings — with fractional seconds and an offset, without fractional
seconds, and (for plain `DateTime` members like `createdAt`) with no offset at all. All three parse;
the last is read as UTC, which is what the server stores.

**`CalendarDay` is not a `Date`.** The server derives a session's day at the offset it was logged
at, deliberately, so that an evening session west of Greenwich does not land on tomorrow and join
against the wrong night's sleep. Turning that day back into an instant on the device would re-open
the question it already answered, so `DateOnly` decodes into a type with no time of day in it at
all.

## Failure: one vocabulary per layer

`APIError` is the server's `Problem` vocabulary as it reaches HTTP, plus the two failures that are
the client's own (transport, decoding). `ProblemDocument` reads both RFC 7807 shapes — a 400's
`ValidationProblemDetails.errors` is just a problem document with one more member.

Each feature restates `APIError` into its own enum **once**, at a boundary: `LoadFailure` for
screens, `SignInFailure` for the sign-in form. No view ever matches on a status code.

Two rules inside that restatement earn their lines:

- **Cancellation is never a domain failure.** `URLSession` reports it as `URLError.cancelled`, an
  ordinary-looking transport error; `HTTP.send` converts it to `CancellationError`, and every
  restatement rethrows it before classifying anything. Without this, every dismissed screen becomes
  a red banner.
- **A 404 reads as *not found or not yours*.** Ownership is checked on every route and another
  athlete's record is deliberately indistinguishable from one that does not exist. The client must
  not translate that into "forbidden" and disclose what the server refused to.

A 409 is a **domain refusal** — a transition asked for from the wrong state — and the UI states it.
It is never swallowed and never retried.

## Load state: a failed refresh keeps the value

`LoadState` has four cases: `.loading`, `.loaded`, `.unavailable`, and `.stale(value, staleness)`.
The fourth is the point. Pull to refresh in a basement is the common case, and blanking a loaded
screen because the network dropped is both the easy thing to write and the thing an athlete notices
immediately. The transition rules are pure functions, `applying(_:)`, so no store re-decides them.
`Staleness` is two sentences: a refresh the server answered with a fault (its own words), or a
server that could not be reached, where the screen shows what the device last knew and the one
useful thing to say is when that was — "As of 14:02", instrument-style.

### Remembered reads

Every read a screen depends on — Today (keyed by the day shown, so the current day keeps one answer
whatever its date), Train's plan, History and its record chains, the focus pins, the records a
movement or a session established, Body, the movement catalogue, the
record summaries and the profile gate — goes through `APIClient.get(_:remembering:)`. It keeps the
response **as the server sent it** under the signed-in account in `ReadMemory`, and when a request
never gets an answer it decodes the kept bytes instead and answers `Remembered.recalled(value,
asOf:)`; a store folds that into `.stale(value, .unreachable(asOf:))`. Keeping bytes rather than
values means no wire type grows an encoder, and the lenient decoder reads an old answer the way it
reads a fresh one. A server that *answered* with a fault is reachable and its failure is thrown as
usual. These reads wait `HTTP.readPatience` (10 s), not the system's minute: a Mac asleep behind
Tailscale leaves a connection hanging rather than refusing it.

The memory is per sign-in, not per device: answers live under an id minted after a sign-in and
deleted with everything under it at sign-out, and a request that lands after a sign-out keeps
nothing. A chat answer is the model reading the server, so sending needs a connection and the
composer says so; the thread's newest page and the memory list are remembered like any other read,
so what Lionheart last said can be read back with no signal.

### First paint, and coming back to a tab

A screen seen before never opens on placeholders. Today, History, Body and the movement catalogue
read their parts from the device first — the same endpoints, windows and keys, run inside
`APIClient.onDevice`, which answers every remembered read in it from `ReadMemory` without a
request — and paint that answer as `.stale(value, .recalled(asOf:))` while the server is asked. The
server's answer replaces it in place; no answer turns it into `.unreachable` as before. A recalled
screen gets no row of its own (one that came and went would move every row twice); if the server
has not answered within a second, `recalledNote(_:)` floats the time it dates from over the top.
The skeleton rows remain for a screen the device has never read.

A tab's `.task` runs on every return to it, and History's read is the whole log and every record
(about 1.2 MB against a real archive). So Today and History read on return only when
`ReadFreshness` says the answer may have moved: nothing current on screen, a write accepted from
this device since the read (`WriteLedger`, counted in `APIClient`, so a set pushed from the workout
or a skip on Train counts without either knowing who shows it), a new calendar day, or an answer
older than the screen's span (5 minutes for Today, 15 for History). Returning to the foreground asks
the same question of both. Pull to refresh always reads.

Today reads ahead once a current day lands: the 90-day trend behind every figure's page, one
request for the days and one for the nights, so the drill-down most often opened is on screen
without a spinner. A trend read for one figure fills every figure the same read answers.

### Knowing the server is there

`ServerReachability` learns from the requests the app actually makes, not from the phone's network
state: any answer of the server's own means reachable, no answer means not. `tailscale serve`
answers a 502 of its own, with no problem document, when the backend behind it is down, and
`HTTP.isGatewayStandIn` reads that as no answer too. While the server is away and the app is in the
foreground it probes with one cheap read on a widening interval (5 s up to a minute), and at once on
returning to the foreground. When the server answers again `AppContainer.serverReturned` runs in
order: the workout in flight syncs (a deferred start opens its session), the outbox delivers, and
every screen not current is read again. There is no push on the free developer tier, so nothing
happens while the app is suspended.

## Auth: one refresh, whatever the burst

Tokens live in the **Keychain** (`kSecAttrAccessibleAfterFirstUnlock`, not synchronised), never
`UserDefaults`: a refresh token is a long-lived credential and a plist in the app container is
readable by anything that can read the container.

`TokenProvider` is an actor because renewal must not happen twice. Parallel loads all meet a
just-expired token, all get a 401, and all ask for a renewal; a second refresh with a token the
first has already rotated away would fail and sign the athlete out mid-session. The actor
serialises the *decision*: the first caller through starts one task, the rest await it, and a caller
whose stale token is no longer the one held simply takes the new one without a request. The renewal
itself runs `nonisolated`, so the actor is not holding a queue for the length of a round trip.

It performs its own `POST /refresh` through the shared `HTTP` helpers rather than calling back into
`APIClient`. That is what keeps the dependency one-way — the client reaches the token provider and
never the reverse — without duplicating how a status becomes an error.

`APIClient` retries a 401 **exactly once**. A second 401 after a fresh token means the credential is
not what is wrong.

Two things about sign-in that are easy to get wrong:

- **`POST /login` is sent without `useCookies`.** With it, Identity succeeds and returns a body with
  no tokens in it.
- **Code signing must stay on, even for the simulator.** `CODE_SIGNING_ALLOWED = NO` strips the
  application-identifier entitlement and every Keychain write fails with `errSecMissingEntitlement`
  — the athlete appears to sign in and is signed out again on the next launch, with nothing saying
  why. `AuthStore.isSessionPersisted` now surfaces that failure in Settings rather than letting it
  be silent.

Anything scoped to a session is torn down in `AuthStore.signOut`. Stores register their own
teardown, so this type never has to know what screens exist.

## Today: one window, four answers

Three requests in parallel (`async let`): sessions, Oura days and Oura nights. On today, a single
wide session window — 90 days back, 45 forward — gives the last completed session, the next planned
one and anything in progress, so the cards cannot disagree with each other the way three separate
reads could. `TodaySnapshot.reading` does the picking, purely.

### Any day, not only today

The day steps back and forward from the toolbar, and a calendar jumps further. The selection is
`DaySelection` — `.today`, which follows the clock across midnight, or a fixed past day — and there
is no future case, because a day that has not happened has nothing to read. A past day reads only
the sessions around it and keeps those dated on it; the questions about *now* (next, in progress,
the plain-words log) are answered on today only. Changing the day drops the old snapshot rather
than keeping it as stale: a stale value is the same day not yet refreshed, and one day's recovery
under another day's title would be wrong, not old. The server already serves everything per day
(`/days`, `/sleep` windowed on the clock, the session window), so no new read was needed for it.

### Recovery against his own baseline

The screen leads with readiness as the verdict and then a table of recovery figures — HRV, lowest
heart rate, time asleep, breathing from the night's main sleep; sleep score, temperature, steps from
the day — each with its deviation from the 28 days before and a sparkline of them. The baseline
excludes the day itself, needs seven readings, and a figure outside one standard deviation in the
worse direction is the one thing coloured (orange). These are **measurements** from the sleep
period, deliberately not the readiness contributors called "HRV balance", which are already Oura's
comparison and would make the deviation a comparison of a comparison. Temperature is likewise
already a deviation, so it shows no second one. Every figure opens its trend: daily points, a
seven-day mean, over 30 days, 90 days or a year, read on demand and held by key in `TodayStore`.

### The night and the day under the figures

Under the figures sits the night they were read from — the hypnogram as a strip, with how long and
how efficiently he slept — and the shown day's movement as a strip of Oura's five-minute classes.
Each opens its own screen: the night's stages, stage shares and overnight heart-rate and HRV curves
on one time axis; the day's timeline with its long sittings shaded and listed. The night is the
newest main sleep on or before the reading's day, labelled with its date when it is not that day's,
because Oura files a readiness some mornings before it files the night. The day's timeline is
`GET /api/habits/days/{day}`, read through `HabitsStore` beside Today's own reads.

### Oura sync

`OuraSyncStore` is app-wide, because Today and Body both show the control. A sync reads the
coverage (`GET /api/oura/days/latest`), syncs from the newest stored day **inclusive** through
today, then reads the coverage again; the newest day is re-pulled because it is usually still
accruing, and the server's upsert makes that free. Coming back to the foreground syncs when the
store is behind — before today, or at today without the night's readiness, since Oura files a day's
activity before it scores the sleep — at most once per half hour whatever the outcome, so a rejected token is not asked
about on every return. **A rejected Oura token arrives as a 502** with the server's sentence naming
the token — never a 401, which would be this app's own session — and the row shows that sentence.

The window reaching into the future works because the server matches each lifecycle state on its
own time-truth: a completed session on its stamped start instant, a planned or skipped one on the
day it was planned for.

Two derivations are judgement calls worth recording:

- **A completed session dated after today is not "your last session".** Logging after the fact is
  the common case and nothing stops a mistyped date.
- **A planned session dated before today has been missed**, not scheduled, and belongs to History.

### Readiness, and why a window rather than today

Absence is honest on every Oura route: a collection the ring filed nothing for arrives `null` on
the day, a day it recorded nothing for is absent from a range, and asking for that day by date
answers 404. So nothing in the client second-guesses a value — a readiness of zero is one Oura
issued, and `latestReadiness` keeps it.

Today still reads a **window** and takes the most recent day carrying a score, and the reason is
the screen rather than the route: the reading's day is rendered beside the number, so an athlete
who left the ring in a drawer for two days sees "78, three days ago" instead of nothing at all. A
single-day read would make that day field pointless and would show silence on every morning before
the ring syncs.

Oura's readiness contributors are **scores, not measurements**: each is 0–100 and compares the day
against the athlete's own baseline, so `restingHeartRate` among them is how his resting rate
compared and is not a heart rate. Every one of Oura's ratings is typed `OuraScore`, so a score
cannot be drawn or averaged as if it were measured without the code saying so.

### Six contributors, each beside what it rests on

Under the verdict Today picks six readiness contributors that answer "how recovered am I" from the
night — HRV balance, resting heart rate, recovery index, sleep balance, previous night, body
temperature — and pairs each with the measurement it rates (`ReadinessContributorReading`): last
night's HRV and lowest heart rate and time asleep against his 28 days before, the moment the
overnight heart rate bottomed out as a share of the night (what recovery index rates), and the
fortnight's mean time asleep and in bed (what sleep balance rates; Oura's sleep need is not in its
API, so no need is shown). The score is drawn as a mark on a 0–100 track with his usual band and the
measurement as a number with its unit, so the two never read alike; the section header names the
score column "Oura" and an info sheet explains both. The figures no contributor carries — sleep score
(marked as Oura's), breathing, steps — follow as rows. Activity balance, previous day's activity and
sleep regularity rate the day before and the routine, and stay on the Oura day's own screen. On a
morning when Oura has filed readiness before the night, a night-based row says the night has not
synced rather than that no measurement exists.

A figure that accrues through the day — steps — is shown on today as it stands but neither
compared with his baseline nor charted until its day is over: the morning's count against 28
whole days would read as a bad day every morning.

## The active workout

The only state the device owns. `ActiveWorkoutStore` writes the whole value to a small JSON document
in Application Support — atomically, and **before** publishing it, so a crash between the two cannot
lose something the athlete has already seen confirmed. The directory is excluded from backup: a
half-finished workout restored onto a new phone weeks later is noise, and the server holds everything
that matters once the session completes.

Two states that look alike and are not: `ActiveWorkout.sync` is about the **server** — four cases,
of which only `failed` and `refused` are worth saying out loud — while
`ActiveWorkoutStore.isPersisted` is about the **disk** (a force-quit would lose it). They fail
independently and want different words.

### Starting and finishing with no connection

Starting a workout needs no server. `TrainStore` gives `POST /active` or `POST /{id}/start` six
seconds; a start that cannot reach the server begins the workout on the device as a **deferred
start** (`WorkoutDestination.deferredStart`), logged exactly like any other, holding the plan it came
from. The first push that reaches the server opens its session (`WorkoutDelivery.open`) and moves
the workout onto it, so a relaunch after that is an ordinary live workout.

Opening must never happen twice, because a request can land and its answer be lost. So before it
writes, every step that could repeat earlier work first asks what the server already holds
(`ServerCopy`), recognising the device's own workout by its **start instant**, which the device
chose and the server stores as sent — the one key both sides share without a client-chosen id. A
completed session must also hold the workout's set count, because a past workout's start is a time
the athlete picked to the minute and two can share one. The plan is read first: still planned, it
is started; started elsewhere at another instant, skipped or gone, the workout opens a session of
its own and the logging surface says so. A refused plan start is read back before it is believed,
since the 409 may be the device's own earlier start landing late. One opening is in flight at a
time, and a finish or discard holds pushes off while it hands the workout over.

Finishing with no connection is not refused: the finished workout is set down in the **outbox**
(`WorkoutOutbox`, one file per workout under `finished-workouts/`) and the active slot is freed.
Train lists what is waiting under "Not yet on the server". Delivery is `WorkoutDelivery.deliver`, the
same transaction the finish runs, safe to repeat for the same reason opening is. A workout never
opened lands whole through `POST /completed`; a live session deleted elsewhere does too. The server
being away stops a delivery run; a fault of the server's skips that one and tries the rest; a 409, 400
or 404 marks it refused, with the server's words, for the athlete to discard. A finish or discard
waits on the server for twelve seconds and then settles without it — longer by up to ten when a
token renewal is already out, which a deadline cannot cancel because other requests share it — and
marks the server unreachable, so the probe notices its return and the outbox goes out then.

Train leaves out of its plan every session the device is already holding — a plan started offline is
still planned on the server, and a session finished offline is still active there.

Signing out deletes the workout and the outbox, and the confirmation says what that loses. A
sign-out the **server** forces, by refusing the refresh token, keeps both: `AccountCustody` records
a digest of the signed-in email, and the next sign-in keeps them for the same account and empties
them for any other.

`ActiveWorkout.version` exists so a document written by an older build is recognised as an older
shape, and one written by a *newer* build is refused rather than half-read — a downgraded build that
half-understood a future shape would write it back truncated, which is how logged sets get lost for
good.

**A movement is in the workout once it is prescribed or added, not once it has a set.** The logging
surface draws one section per movement and the entry row lives inside it, so a movement the workout
does not hold has nowhere to log its first set. A planned session's prescriptions put its movements
there; an unplanned workout has none, so picking a movement records it in
`ActiveWorkout.addedMovementDataIds`, persisted like a set so a force-quit between picking and
logging loses nothing. A pick with nothing logged on it can be removed; one with a set cannot,
because the set is the record; `ActiveWorkout.canDrop(_:)` is that rule, stated once. Picking a
movement the workout already holds moves the athlete to it without moving its section. Version 3 of
the document added the list; version 4 added the deferred start and the plan a workout came from; version 5 added the routine and a kind on every lift. A version 2 document whose current movement no set or target names
restores with that movement added: usually a pick not yet logged on, occasionally a movement whose
only set was deleted, which comes back as an empty section the athlete can remove.

### The bar, and both OS paths

`ActiveWorkoutBar` is this app's now-playing bar. One view renders it on both paths —
`tabViewBottomAccessory` on iOS 26, `safeAreaInset(edge: .bottom)` on iOS 18 — so there is one
appearance to design.

**On iOS 26 the accessory is hung only while a workout is in flight.** The slot draws its own
capsule whatever is inside it, so applying the modifier unconditionally and deciding emptiness in
its content — which is what the first version did — left an empty glass pill floating above the tab
bar on every screen. Making the modifier conditional changes the `TabView`'s view identity the
moment a workout starts, which would reset the selected tab underneath the athlete; the selection is
therefore `@State` held *outside* the `TabView`, so it survives the rebuild. That is the trade: one
piece of state lifted, in exchange for the slot not being drawn when there is nothing to put in it.

The iOS 18 path still decides inside the inset, where an `EmptyView` genuinely costs no height, so
no workout costs no space.

The elapsed clock is a `TimelineView(.periodic)` inside the bar, so a one-second tick redraws the
clock and not the set list that will sit above it.

**Only the iOS 26 path has been seen running.** No iOS 18 runtime is installed on this machine, so
the `safeAreaInset` branch is compile-verified and `#available`-guarded but not observed.

### The Lock Screen's copy, and keeping the two in step

The same workout also runs as a Live Activity, drawn by the `LionheartWorkoutActivity` widget
extension — a second representation of the device's one piece of owned state, living in another
process. The failure that shape invites is the two disagreeing: an activity counting down a rest
that ended, or outliving a workout that was discarded. Three decisions prevent it.

**The clocks are never numbers.** `WorkoutActivityAttributes` carries instants, and WidgetKit
renders the counting itself from a date range. A suspended app therefore still shows a correct
countdown, and nothing has to be pushed once a second — which also happens to be the only design
that works here at all, since updating a Live Activity from outside the app needs an APNs push and
the owner is on the free Apple tier.

The one change the system cannot count to is the end of a rest: the app is suspended, so nothing
is awake to take the countdown down, and it used to sit at 0:00 until the phone was unlocked. The
content therefore carries the rest's end as the activity's **stale date**, and the extension draws
stale content as working — elapsed clock and lifting glyph — which is what the app's own update
will say once it next runs.

**One funnel, one instruction.** `ActiveWorkoutStore.mirror()` is called from `adopt`, `restore`,
`clearLocally` and `reset` — every way the workout can change — and hands
`WorkoutActivityProjection.of(…)` to the presenter. That projection returning `nil` *is* the
instruction to end the activity, so no caller has to tell "no workout" apart from "a workout that
no longer warrants one", and there is no ordering of events that leaves one behind. Completion,
discard and `AuthStore.signOut` all arrive through those four calls; an activity surviving a sign-out
would leave the last athlete's session on the Lock Screen of a signed-out phone.

**The chain is serialized.** Each `mirror()` awaits the previous one, because two mutations a few
milliseconds apart could otherwise have their cross-process writes land in the other order. The
projection is derived synchronously at the mutation, so each link carries the state it was made for.

A stale activity from a previous launch is reconciled on `restore()`: `WorkoutLiveActivity` reads
`Activity.activities` rather than trusting its own per-launch handle, adopts one whose `sessionId`
matches the restored workout, and ends every other. Everything it does is best-effort and silent —
the athlete can switch Live Activities off, and the Lock Screen is a mirror, never the record.

Two things cost more than they look. `NSSupportsLiveActivities` has to be in `info.properties` in
`project.yml`; as an `INFOPLIST_KEY_*` build setting it never reaches the app's own `Info.plist` and
`areActivitiesEnabled` then answers false with nothing in the log saying why. And `ActivityKit.Activity`
is deliberately not `Sendable` while its `update` and `end` are nonisolated `async`, so under this
project's `NonisolatedNonsendingByDefault: off` every isolated caller is a sending error;
`LiveActivityHandle` is where that escape is contained and documented.

### Hearing the rest end

The rest clock is a stored `Task`, which stops running the moment the app is suspended — so a phone
in a pocket between sets would show a correct countdown and never announce the end of it. The alert
is therefore held by the system, as a local notification scheduled for the rest's end instant.
**This needs no entitlement, no APNs and no paid membership**, which is what makes it the alert this
app can actually have; only a *remote* notification needs those. Measured on the simulator: an
ad-hoc-signed build with no team scheduled a 20-second rest alarm, and the system delivered it with
the app backgrounded.

It is a third representation of the same state, in the system this time, with the same failure as
the other two — one left behind, buzzing about a rest that was skipped or a workout that was thrown
away. So it goes through the same `mirror()` funnel, as `PendingRestAlarm.of(…)`, and `nil` is again
the instruction to withdraw. Nothing in the store schedules or cancels directly.

Two places it differs from the Live Activity, both deliberate. It is derived from the **rest** and
not from the activity's projection, because a workout written down afterwards warrants no Lock
Screen entry but is logged through the same surface and can arm a rest just the same. And sign-out
additionally calls `forgetDelivered()`, because an alert that already *fired* is sitting in
Notification Centre naming the last athlete's movement, which withdrawing a pending request cannot
reach.

Authorisation is asked for at the **first armed rest**, not at launch: that is the moment the
permission is obviously for, and an app that prompts before the athlete has used it earns a refusal.
A refusal costs the buzz and nothing else — the rest clock, the bar and the Lock Screen countdown
are unaffected, which was checked by running with the prompt unanswered. A rest that ends while the
app is in the foreground does not double up with the in-app haptic: with no
`UNUserNotificationCenterDelegate` the system does not present a notification to a foreground app,
which is the behaviour relied on here.

**How it has been seen.** `simctl io screenshot` photographs the Island as an empty capsule, but
`XCUIScreen.main.screenshot()` from a UI-test runner composites it, and the runner can long-press
the Island and pull down the Lock Screen through SpringBoard. Compact, expanded, the Lock Screen
before and after a rest ran out in the background, the rest-end notification, and the activity
ending on Finish, discard and sign-out were all seen that way. The minimal presentation needs a
second app's activity on the Island and has not been seen.

## Logging: routines, last time, set kinds and bodyweight

What makes logging a session as fast as Strong for an athlete who trains two sets to failure on an
upper / lower / full-body split.

### Routines

A routine is the server's own aggregate (`docs/data-remodel.md` says why it is not a planned session
or a program): a name, an order of movements and a working-set count each — no weights. Train lists
them under "Routines", and a tap starts a workout from one exactly as "Start workout" does, through
`POST /active` with the routine's id, or as a deferred start when the server cannot be reached. The
routine is copied into the workout document (`ActiveWorkout.routine`, version 5) and its movements
are added to the workout in order, so each has a section before anything is logged and one can be
removed like any pick. The header's "1 of 2" counts working sets against the routine's count where
nothing was prescribed. The session the server opens — or the one `POST /completed` lands — names the
routine; a routine deleted meanwhile lands the session ad hoc rather than refusing it.

The fastest way to make one is from a session already trained: "New routine → From a past session"
lists the sessions Train's window holds, and the server takes their movements in order with each
one's working sets, and makes that session the routine's first. The editor reorders, removes, adds
from the same picker and sets counts. The finish readout offers "Save as routine" for an ad hoc
workout and "Update ‹routine›" when today's movements or counts differ; a routine movement left
untrained today keeps its count, since running out of time is not a decision to drop it.

### Last time

`GET /api/training/previous-performances` answers, for every movement, its sets from the most recent
completed session holding it, and `MovementLibraryStore` keeps it beside the catalogue and the
records — remembered, so a routine started in a basement still has last time — and read again when a
finished workout lands, straight away or from the outbox (`WorkoutOutbox.onLanded`). A session
that only warmed up on a movement is not last time for it. Each movement's
section shows it as one line, and the entry row opens on it (`SetDefaults`): the first set on last
time's first working set; later sets on today's weight with last time's reps at the same position,
which is the number a set to failure is trying to beat. A plan's target still beats last time, and a
warm-up is never a precedent, so the set after a ramp opens on the work. The row's guide line says
"Last 160 × 6" only once the fields differ from it.

### Set kinds

A lift is working, warm-up, drop or failure. Warm-ups are numbered apart and marked "W", answer no
target, count toward no total and never badge a record — the server keeps them out of every record
for the same reason. History, a movement's page and the training load read finished sessions the
same way: their counts, totals, lines and charts are the work. A drop set counts toward totals and
records but takes no place in the plan (`SetKind.takesAPlace`): it extends the set before it, so it
answers no target, does not raise a routine's count, and the set after it is read against last
time's next working set. The kind is chosen in the entry row's set-type menu, beside the measure, and a
warm-up ramp logs its steps as warm-ups.

### Plates and warm-ups

On a barbell in pounds, the guide line gives the plates per side for the weight typed (45 lb bar;
45, 35, 25, 10, 5 and 2.5 lb plates; not configurable). A movement's menu offers "Warm-up sets": a
short ramp to the working weight — the bar, then about half, seven-tenths and most of it, each
rounded down to what the plates make — which is logged a step at a time or all at once, and starts
no rest. A weighted dip ramps from bodyweight alone. It is offered, never inserted.

### Bodyweight

One reading a day, logged on its own screen under Train (a `PUT` to the day, so a retry writes the
same row), with its trend over 30 days, 90 days or a year in `DailyTrendChart`. It lives in Train
because that is where it is weighed against: a movement whose setup says its weight is added to
bodyweight shows "System 225 lb" — the reading in force on the workout's day plus the plate — in its
entry row, and the finish readout judges it on that load. `BodyweightSeries.inForce(on:)` and
`SystemLoad` are the pure functions any screen judging a past dip needs. Body, the Recovery tab, can
link to the same screen.

### A movement's setup

A movement's menu holds "Setup and notes": the athlete's cues ("seat 4, pin 7"), shown under the
movement while logging, and whether its weight is added to bodyweight. They are the server's
(`PUT /api/movement-datas/{id}/setup`), so a change with no connection is refused in the sheet with
the text kept. The same menu opens the movement's page from History over the workout.

### The finish readout

`WorkoutSummary` is built when Finish is tapped, before the hand-over: once the server has the
session, last time on every movement is this workout. The root presents it once the logging surface
has gone. It reads duration, working sets, volume and the sets that beat a standing record; the last
session of the same routine as the same figures with their changes; and each movement's best set
against last time's, as a change in estimated one-rep max — on bodyweight plus plate for a weighted
dip when both days have a reading in force, and on the plate alone otherwise, so a day with no
bodyweight never reads as a leap.

## History: the training log, a movement's page, and pinned movements

**The log reads as a diary.** It is grouped by Monday-to-Sunday week, newest first — the unit his
first goal is measured in — each week's head its dates (opening the week) and its session count,
each row a day (opening the day). A run of weeks with nothing logged is one quiet line between the
weeks either side, a fact rather than a reproach. The day page (`DayView`) holds the ring's scores
and measured line, every workout — the log's sessions, with a logged run's zones from the cardio
report, and the ring's own workouts — the day's movement and his wellness rating; the week page
(`WeekView`) holds lifting by session, cardio by zone, the ring's means and the seven days. Both
step to the next or previous day or week from the toolbar. Their ring, cardio and wellness reads go
through `DiaryStore`, keyed by day or Monday; the lifting is the log `HistoryStore` already holds,
so a correction to a session shows on its day at once. The consistency calendar opens any past day.

**A completed session can be corrected** from its detail (`SessionRevisionView`), with Train's own
set pieces: `EditSetSheet` for a set, `SetEntryForm` for a new one, `MovementPicker` for a movement.
The draft (`SessionRevisionDraft`) addresses sets by a device id and keeps a picked movement as an
empty block until its first set, as the workout in flight does; moving the start keeps the length.
Saving is `POST /{id}/revise`, which restates the session whole and rebuilds the records when the
sets or the start changed — the screen says so before saving. **A correction needs the server and
is not queued.** It is not work that could be lost: the session is already safe on the server. And
the server can refuse it — while any record is withdrawn, since the rebuild would award it again —
so a queued correction could be turned down later with nobody watching. With no connection the
sheet keeps the edit and says that saving again is safe: a correction restates the session whole, so
one whose answer was lost is not doubled by a second. A session holding a set or a period this build
cannot state refuses to open the editor rather than lose it on save. Record chains and marks held on
the device are dropped after a correction, because a rebuild gives every record a new id.

History is where training is found again, and its first read is the athlete's **whole log** — every
session from the epoch through 45 days ahead, about half a megabyte for two years and 206 sessions.
Everything else on the tab is a reading of that one answer, so it costs no request of its own: the
log, the consistency reading, the movement index, and every movement's history, charts and
frequency. A per-movement route would have been a second account of data the device already holds,
so none was added. `HistoryStore` is the app's one reading of the log; Today opens a movement's page
from it too, through the same `HistoryDestination`, so a movement reads the same from either tab.

**The log is a log book.** A completed session's row is its day and duration, each movement's
`MovementDigest` line, and its session total — the volume per unit, since units never convert. The
filter by movement is a search token: typing suggests movements as completions (iOS 26 does not show
`suggestedTokens` while the field is minimized into the toolbar), and a chosen token keeps the
sessions that performed that movement **by id**, so a paused squat never brings in the high-bar
ones. Free text still matches names and notes.

**Consistency is plain measurement.** Sessions per week over a span, the four-week average, the
weeks trained, the median sessions in a week that had any, the current and longest run of weeks
with a session, and a calendar of training days back to the first. The run is strict — one empty
week ends it — and the week in progress cannot end one on a Monday morning. Nothing is coloured for
a week missed.

**A movement's page** is Strong's History, Charts and Records under a summary that is always there:
sessions, sessions a week since the first and over the last twelve weeks, the last day, and the
best estimated single. The estimate is Epley with **no estimate above twelve reps or at zero load**,
which is the athlete's own analysis's rule; the server's records carry an uncapped Epley, which is
harmless there because a record never sits above the twelve-rep max. Charts read one measure at a
time in one weight unit, with gaps where a session had nothing to measure. Records reuses the
records screen's sections (`SubjectRecordsSections`).

**Record marks come from the server.** A set is marked when a record the server established — any
standing, `GET /api/personal-records/established` — names it: the session that began at the
record's instant, the movement, the load, the reps and the unit. The device does not re-run
detection. A first of its kind marks nothing, for the reason the live badge stays quiet about one,
and a withdrawn record marks nothing because the athlete said it was not real. A best session volume
belongs to the session's work on a movement and is said under that movement, not on a set.

**Pins made for emphasis are read here.** Sessions done against the frequency a pin asks for, the
estimated single before the horizon against the best inside it, the heaviest load at the target's
reps against the target: each a reading of the log (`FocusReading`), so a session logged late can
never leave a stored "met" disagreeing with it. The server stores only the pin, its role and whether
it is still pinned. History's Priorities row opens the screen described under Train.

### Naming every movement

A set carries only a `movementDataId`, and the catalogue loaded at sign-in knows nothing of a
movement the Strong import or the web app made since. So the catalogue is one app-wide value in
`MovementLibraryStore`, and a screen holding sets asks it to **cover** them: History after each
read, Today keyed on the movements its sessions name, and any `title(of:)` that misses — which is
how Train and the workout bar are covered without each asking. A miss reads the catalogue again
once; an id a current catalogue still does not hold is remembered as unlisted and not asked about
again, so a deleted movement costs one read rather than one per render. A read that could not reach
the server proves nothing and leaves the id to be asked about again.

## Train: what it is time for, the week's volume, priorities and the score

Train leads with **Start workout** and **Log a past workout**, then answers what it is time for.
Routines, bodyweight and the plan follow; they stay, below the reading of the week.

**Everything on the week is the server's reading.** Weekly volume (`GET /api/training/volume/{day}`),
the priorities (`/api/training/priorities/{day}`), the training score and its weeks are derived on
the server, which holds what every movement trains and the bodyweight a weighted dip's system load
is built on, and which hands the same answer to the chat model. `TrainingBalanceStore` reads all four
for the seven days ending on the device's today, each into its own load state and each remembered,
so a failed score read never blanks the volume and a launch with no signal opens on the last week
read. It is supplied through the environment, which is how Today carries the score in one line and
the classification sheet refreshes the week after a change, without either screen's initialiser
knowing.

**Seven days, not a calendar week.** Every figure on the week is the seven days ending today, so
nothing empties on a Monday and the number reads the same whichever day it is opened. History's week
page asks the same routes for the seven days ending on its Sunday, or today for the week in
progress (`TrainingBalanceStore.loadWeek`, drawn by `WeekBalanceSections`), so a week there and the
week on Train agree when they are the same week.

**Up next** names the family or families trained longest ago — together when last trained the same
day, which is how an upper day followed by a lower day leaves push and pull up next as one — with
their week's hard sets. Oura's readiness score is mentioned only when it is under the tenth
percentile of his last ninety days, beside the recommendation, never instead of it.

**Volume is one instrument at three resolutions**: push, pull and legs on the tab, then the nine
patterns and the sixteen muscle groups behind a link, every row the same `VolumeRow` — the name and
when it was last trained, hard sets of the target, and a `TargetBar` that is grey under the target
and green at it. Movements nothing classifies are listed as not counted rather than dropped.

**Priorities** (`PrioritiesView`) is the one place that says what he is striving for: each family's
priority lift with its best estimated single against the twelve weeks before, a run of session bests,
and its sessions in four weeks; the week's zone 2 and high-intensity minutes against their targets;
then the pins made for emphasis and the past ones. With no priority pinned the family's most-trained
compound lift of eight weeks stands in, and says so. Setting a priority picks from the family's
compound lifts (`PriorityCandidates`), and pinning one lets the last go on the server in the same
write.

**The score** is a number with its measurements: Today shows it beside push, pull and legs hard sets,
zone 2 and the week's sessions; its screen shows half a year of weeks against 90 and every part as
"x of y" rows. A part with no data says unknown, never zero, and a week read with one is drawn pale.

**What a movement trains** is shown and edited on its page (`ClassificationSection`), stated for the
base so every variation shares it. A judgement call from the shipped map is shown in orange with its
reason until he confirms or corrects it.

## Personal records: one unit at a time

The server establishes records when a session's sets become final; the client reads them, shows
them, and withdraws one when the athlete says the set was not real. Three rules shape it:

- **The unit is part of a record's identity.** A movement logged in pounds and kilograms holds a
  record of each kind in each, and weights never convert. Everything that groups or looks records
  up keys on `RecordKey` — subject, kind, unit — never on the kind alone. The movement screen reads
  one unit at a time behind a segmented control, and a history narrows the route's every-unit
  answer to the record's own key. Distances carry no unit in their key: they rank in meters.
- **The rep-max grid is a grid.** One cell per rep count from one to twelve, held or not, so an
  untested count reads as a gap rather than closing up. The server orders a summary newest first,
  so the rep order is made on the device. The first cell is the heaviest-set record — every set
  contains a single, so the server keeps no separate one-rep max — and the heaviest set is
  therefore not repeated as a row above a grid that holds it. A set holds the rep max at its own
  count and every count below it, so a cell held by the front of a longer set says "set of 7" under
  its weight; the achievement decodes the rep count and the set's own reps apart. A movement whose
  only lifting record is its heaviest set shows that as a row rather than one cell beside eleven
  dashes.
- **The live badge says only what beat a standing record.** A set competes for the heaviest set,
  the rep max at every count it holds and the best set volume, in its own unit, against the records
  as they stood when the workout began and the movement's earlier sets in this workout — so the
  badge lands on the first set that beats a record, and a repeat of that set lights nothing. Firsts of their kind, session volume and the distance
  records are left to the server, which decides them on completion — so one set of twelve on a new
  movement lights nothing rather than eleven badges. Several records beaten read as one sentence,
  the most worth saying first: "New heaviest set and 5 more records".

## Body: the scores, cardio, daily movement and body composition

Body leads with the latest readiness and the last night, then the latest day's scores, then three
questions with screens of their own.

- **Scores and contributors.** A day's readiness, sleep, activity and resilience are drawn as an
  instrument: each of Oura's components on a 0–100 `RangeGauge` against the band he usually sits in
  (the 28 days before, mean ± one spread) with the mean as a tick. A component is already Oura's
  comparison with his baseline, so the useful reading is where today sits against where he usually
  sits, which a bare number cannot show. Measurements that are not components — temperature,
  steps, SpO2, stress, VO2max — stay as figures.
- **Cardio** is its own screen because it is its own question — "is my aerobic base improving",
  with trail running a stated goal — and carries what no section of Body could: a span, a stated
  heart-rate zone method, and sessions to open. It is the server's `GET /api/cardio` drawn: the
  aerobic-base table (four weeks against eight), weekly minutes stacked by zone, Oura's VO2max and
  cardiovascular-age estimates labelled as estimates, the nights' lowest heart rate, and every
  session with a zone bar, opening onto its heart-rate curve against the zone bands
  (`GET /api/oura/heartrate`).
- **Daily movement** is `GET /api/habits` drawn: weekday and weekend means against the span before,
  orange only where a change went the worse way; the mean day hour by hour from 04:00 with the
  hours training began beneath; each day's longest sitting; sleep timing as bedtime-to-wake bars;
  steps; and each day's timeline.

**Every span and comparison says what it is in words.** Spans read "30 days", "90 days", "1 year"
(and "3 months", "6 months", "1 year", "All" on the log), with the days they cover under the
control. The aerobic base is always the last four weeks against the eight before, so it sits above
the span control it does not depend on, its columns say so, and its footer gives both windows'
dates. Cardio and daily movement each have an info sheet with the method — how zones and the maximum
are set and what is left out; what counts as sitting and which days count — rather than spreading it
through the footers. A VO2max Oura never issued is a row and a section left out, not dashes.

**Wellness states** are logged from Body's "+" menu beside a new injury: the day and four 1–5
ratings, each starting unchosen so nothing is logged he did not pick, and a day already rated opens
on its ratings because saving replaces them. Like a correction, a rating needs the server. The
wellness screen (`WellnessStore`) reads the span's ratings with Oura's readiness on one time axis,
the mean readiness on days he rated low, middling and high, and Pearson's r between his overall
rating and readiness once eight days carry both.

Both are server-side derivations rather than `Core/` ones, because their input is the all-day

- **Body composition** is one InBody sheet per scan, drawn as an instrument rather than a copy of
  the paper: the headline figures and their change since the scan before, segmental lean as bars
  against the analyser's own reference (100 is its reference for his height and weight, not a norm)
  with left against right beneath, segmental fat the same way, then the trends across scans — with
  one scan, the trend section says a trend starts with the second rather than drawing a lone
  point — and the sheet's other panels. A scan's weight is that day's bodyweight measurement, so
  the bodyweight screen is linked from here as well as from Train.

  **Adding a scan** reads the sheet on the phone: VisionKit's document camera (or a photo or PDF)
  gives a page, `SheetRecognizer` runs Vision's text recognition over the whole page and over
  overlapping tiles of it, `InBodySheetReader` (`Core/`) reads each pass by the sheet's layout,
  and the passes vote. The athlete then checks every figure in `ScanReviewForm`: an illegible
  figure is an empty box marked "Not read"; a figure the passes read differently, or that the
  sheet's own sums contradict, is marked "Check" with each reading offered or the sum it fails; and
  nothing is saved until every asked figure is filled. No model is involved: the live chat model is
  a local one whose image input is unverified, and a layout reader is deterministic and testable
  against a real sheet's recognised text.

Cardio and daily movement are server-side derivations rather than `Core/` ones, because their input
is the all-day heart-rate series and a year of five-minute classes, and because the chat analyst
reads the same reports as tools; the client derives nothing it could get wrong differently. Each
store holds one report per span, like Today's trends, and reads the span again after an Oura sync.

## Apple Health: runs in, a page each

The athlete records his runs in Strava, which writes them to Apple Health with their routes; the
phone reads them there and offers each to the server. What becomes training, and how a run several
sources saw is counted once, is the server's rule — `docs/apple-health.md` has all of it. The phone's
side:

- **`Health/` is the one door into HealthKit**, a top-level folder beside `Net/` because it is the
  app's second outside source. `HealthLibrary` owns the store and asks for every reader's types in
  one sheet; `WorkoutReader` is its first reader. A later reader adds its types to
  `HealthLibrary.readTypes` and sits beside it. Nothing outside `Health/` sees a HealthKit type: a
  reader hands over `HealthWorkoutImport`, a plain wire value.
- **Read only.** `SampleRuns`, the seeding behind a Settings button, is the one writer, and it is
  compiled only into a Debug build for the simulator — never Release, and never a Debug build on the
  phone, whose Health store is the athlete's own.
- **`HealthSyncStore`** keeps HealthKit's anchor in a file, reads ten runs a page after it, offers
  each, and moves the anchor on only once the page is answered — so a sync that loses the server
  resumes at that page, and the server's idempotence absorbs the overlap. A run the server refuses is
  counted, its id held back and offered again first on the next sync, and the sync goes on; anything
  else stops it. It runs on returning to the app (once per
  15 minutes, and only once connected) and on pull to refresh in History and on Cardio. Connecting is
  a tap on the row, never a sheet sprung at launch. Its row sits beside Oura's on Today and in
  Settings.
- **The run's page** (`RunView`, reached from a session's detail and from Cardio) draws the server's
  `RecordedRun`: the route with MapKit's `MapPolyline` — a still picture at a fixed height, since a
  map that pans inside a list takes the list's scroll and one that grows with Dynamic Type outgrows
  the screen; a tap opens it full screen to move and zoom — the figures, splits in kilometres or miles —
  the one conversion the page makes, a change of unit — and the heart rate against the zones, footed
  with whose it is. It shares the cardio session's chart and zone grid, which moved to `Design/` for
  the second caller.

## Chat: one thread, with memory

The Chat tab opens on the **thread**, not a conversation list. The athlete never starts a chat: he
scrolls back through everything, and the server decides what the model reads — the current
session and what earlier sessions left in memory (`docs/api-surface.md` → Chat thread and memory).
The screen draws the sessions as one transcript; each session's opening is a dated line between
them, the only sign of the cut.

- `ThreadStore` holds two states, not one: the transcript as a `LoadState<ChatThread>`, which can be
  recalled offline, and the exchange (`ThreadExchange`: idle, awaiting a reply, refused). They vary
  apart — a recalled thread can hold a question that failed to send — so a single enum would have to
  multiply their cases.
- Sending answers with the whole session the exchange went into, which replaces the session held or
  is appended when the message opened a new one. Earlier sessions are read on request, by the
  oldest opening on screen, and put in front.
- The standalone conversations (`ChatView`) are pushed from the thread's toolbar. They are where a
  question goes that should not be remembered, and keep their list sheet and new-conversation button.
- `MemorySheet` lists memory by kind, standing kinds first; a memory's page shows the words it came
  from and the versions it replaced, and the athlete can correct it (the old wording becomes its
  history) or forget it (every version goes). A correction has a new id, and the page follows it.

## Design

There is no design system. Apple's is the design system, which is the direct expression of the
owner's brief: native, like Apple Music, and explicitly not the web app's custom look.

- One brand token — the accent in the asset catalog — applied once as `.tint` on `RootView`.
  Everything below inherits it, and light and dark are then correct for free.
- No colour literal in any view. Semantic system colours only.
- System text styles throughout, so Dynamic Type works with no `relativeTo:` anywhere. Figures that
  change in place carry `.monospacedDigit()`.
- `List(.insetGrouped)` is the default screen.

`Design/` holds only what stock SwiftUI has no answer for: `Tokens`, `ActiveWorkoutBar`,
`Sparkline` (a stock `Chart` with every axis removed), `StatTile`, `SetRow`, `RestTimerBar` and
`LoadStateRows` (the loading row and stale banner three tabs share), and `MovementLines` (a
session's movements as log-book lines, figures aligned down one column, which Today and History share),
and `SummaryFigures` (a span's headline figures as columns, or labelled rows when they would not fit),
`LevelTimeline` (states in lanes over clock time — a hypnogram, a day's activity), `RangeGauge` (a
value on a fixed scale against a usual band), `MeasureColours` (the one place a sleep stage, an
activity class or a heart-rate zone gets its colour, so each reads the same on every screen), and
`DailyTrendChart` (a per-day figure's readings, a line through them, the span's mean and a pressed
day — bodyweight's trend, and any other figure read once a day or less), and `HeartRateZoneChart`
with `ZoneMinutesGrid` (heart rate over a session against the zone bands, and minutes per zone — a
cardio session's and a recorded run's).

Colour carries a verdict in one way only: orange marks a recovery figure, or a readiness band,
that asks for attention. Oura's other bands read in the secondary label colour — a traffic light's
yellow does not read as text on a light background — and the accent is kept for what can be tapped
and for the one line on a chart the eye should follow.

A note on the plist: the app supplies its own `Info.plist`, so `INFOPLIST_KEY_*` build settings do
not reach it. `UILaunchScreen` has to be declared there explicitly or the app runs letterboxed in
compatibility mode on every modern device. `NSAllowsLocalNetworking` is the narrow ATS exemption
that lets a Debug build talk to `http://localhost`; `NSAllowsArbitraryLoads` would also permit it in
Release, which is exactly what must not happen.

Another: a `//` begins a comment in an xcconfig file, so `http://host` truncates to `http:`.
`LH_SLASH` exists only to smuggle the two slashes past the lexer.

## Running it against the backend

```bash
ln -sfn "$(dirname "$(git rev-parse --path-format=absolute --git-common-dir)")/.env" .env
dotnet ef database update          # a fresh worktree's SQLite file has no schema
dotnet run --launch-profile lionheart     # http://localhost:7025
```

The launch profile is named `lionheart`, not `http`. The simulator reaches the host's loopback
directly; a physical phone needs `ios/Configs/Local.xcconfig` (copy the `.example`).

An account needs **two** calls, which is the non-obvious part:

```bash
curl -X POST localhost:7025/register -H 'Content-Type: application/json' \
     -d '{"email":"…","password":"…"}'
curl -X POST localhost:7025/api/user/create-profile -H "Authorization: Bearer $TOKEN" \
     -H 'Content-Type: application/json' -d '{"displayName":"…","age":30,"weight":82.5}'
```

A registered Identity account with no Lionheart profile is refused by every training route with a
409 — "This account has no Lionheart profile yet." The iOS client has no profile-creation screen, so
a new athlete currently has to be set up elsewhere. See "Known gaps".

## Known gaps

Deliberate, and left for the agents that own them.

1. **No profile creation.** `POST /api/user/create-profile` has no screen. A freshly registered
   account sees the 409 above on Today, stated honestly but with no way to act on it.
2. **No registration.** Accounts are made on the web app; a second path to the same place is surface
   nobody asked for.
3. **The interpretation confirmation round.** `POST /interpret` answering `awaitingConfirmation` is
   a **success**, and the client reports it as one — but the screen that collects the athlete's
   choices and resumes through `POST /confirm` does not exist. `InterpretationOutcome` already
   carries the draft, undecoded, as a `JSONValue`: resuming means handing the server back exactly
   what it sent, and mirroring the parser's whole output in Swift types would make every member of
   it something the client could silently drop. Re-encoding may reorder an object's keys, which is
   safe only because the server sets `AllowOutOfOrderMetadataProperties`.
4. **An injury event cannot be backdated** — the server stamps it with its own clock.
5. **`PUT /prescriptions` is unwritten.** The rest of the lifecycle — the three creates,
   `/start`, `/complete`, `/skip`, `PUT /sets` and `PATCH /notes` — is in `TrainingSessionsAPI`
   and driven by the Train tab. Nothing in the app plans a session yet, so the one route that
   would need it is left rather than guessed at.
6. **No movement is created on the device.** The exercise library reads `GET /api/movement-datas`
   and nothing writes to it: a movement needs an equipment and a base chosen or invented first,
   which is catalogue curation rather than training, and the web app already does it.
