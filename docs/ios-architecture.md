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

`LoadState` has four cases: `.loading`, `.loaded`, `.unavailable`, and `.stale(value, failure)`.
The fourth is the point. Pull to refresh in a basement is the common case, and blanking a loaded
screen because the network dropped is both the easy thing to write and the thing an athlete notices
immediately. The transition rule is one pure function, `applying(_:)`, so no store re-decides it.

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

Two requests in parallel (`async let`), and one of them answers three of the screen's four
questions. A single wide session window — 90 days back, 45 forward — gives the last completed
session, the next planned one and anything in progress, so the cards cannot disagree with each
other the way three separate reads could. `TodaySnapshot.reading` does the picking, purely.

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

Two numbers on that card are **contributor scores, not measurements**. Oura's readiness
contributors are each 1–100 and compare the day against the athlete's own baseline, so
`restingHeartRate` is how his resting rate compared and is not a heart rate — the rate itself is on
the night, as `OuraSleepPeriod.lowestHeartRate`. `ReadinessReading` names them `…Balance` for that
reason, and the cards that render them say "balance" too.

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

`ActiveWorkout.version` exists so a document written by an older build is recognised as an older
shape, and one written by a *newer* build is refused rather than half-read — a downgraded build that
half-understood a future shape would write it back truncated, which is how logged sets get lost for
good.

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

**What has not been seen.** The Lock Screen and Island presentations were photographed from a
throwaway harness rather than from the device: `simctl io screenshot` captures the framebuffer and
the Island composites its content elsewhere, and `simctl` cannot lock the screen. On the device
itself the activity's creation, its content, its four render passes and its reconciliation at the
next launch were read out of the ActivityKit and WidgetKit logs.

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
  so the rep order is made on the device.
- **The live badge says only what beat a standing record.** A set competes for the heaviest set,
  the rep max at its own rep count and the best set volume, in its own unit, against the records as
  they stood when the workout began. Firsts of their kind, session volume and the distance records
  are left to the server, which decides them on completion.

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
`ReadinessGauge` (the stock `Gauge` plus Oura's band colour), `StatTile`, `SetRow`, `RestTimerBar`
and `LoadStateRows` (the loading row and stale banner three tabs share).

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
4. **Wellness and injuries are fetched by nothing yet.** `WellnessAPI` and `InjuriesAPI` exist
   because the Body tab needs them next; Today does not read them.
5. **`PUT /prescriptions` is unwritten.** The rest of the lifecycle — the three creates,
   `/start`, `/complete`, `/skip`, `PUT /sets` and `PATCH /notes` — is in `TrainingSessionsAPI`
   and driven by the Train tab. Nothing in the app plans a session yet, so the one route that
   would need it is left rather than guessed at.
6. **No movement is created on the device.** The exercise library reads `GET /api/movement-datas`
   and nothing writes to it: a movement needs an equipment and a base chosen or invented first,
   which is catalogue curation rather than training, and the web app already does it.
