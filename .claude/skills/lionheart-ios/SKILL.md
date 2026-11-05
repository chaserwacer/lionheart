---
name: lionheart-ios
description: House conventions for the native SwiftUI client in ios/ (iOS 18+, Swift 6, xcodegen, no third-party packages). Use for ANY work under ios/ — a screen, a store, an endpoint, a fix, a test — and whenever a web screen under ClientApp/src/routes/ is being ported to SwiftUI. Pair it with swift-engineering-standards, which owns the Swift language and design rules; this skill owns where things go in this app, which stock containers to use, how the server's API translates, and what "green" and "looks right" mean here.
---

# Lionheart iOS

Load `swift-engineering-standards` alongside this. It owns modelling, failure values, concurrency,
names and comments; nothing here restates it.

Lionheart is a personal training-intelligence app: it records what the athlete did, pulls in what
their body did (Oura), and lets a language model reason over both. The iOS client is now the primary
way the owner logs training — the web app stays for desk work and is not the reference for how this
should look.

## Read before building

- `docs/api-surface.md` — the route-level spec. Requests are purpose-built commands; responses are
  domain records; **transitions, not shapes**. Read it before writing any endpoint type.
- `docs/data-remodel.md` §"Domain model" — the sealed hierarchies the wire types mirror. The client
  must not flatten them (see the standards skill's "Enumerate domain states").
- `docs/ios-architecture.md` — why each screen and subsystem has its shape. Read the section for the
  area you touch.
- `docs/glossary.md` — words come from here; a noun you coin gets its line in the same change.

## Where things go

```
ios/
  project.yml                  xcodegen; the only place targets, flags and the version live
  Scripts/check-layering.sh    fails when Core/ imports SwiftUI, UIKit or Charts
  Lionheart/
    LionheartApp.swift         @main; builds AppContainer, hands RootView the environment
    Core/                      pure values and functions — no SwiftUI, UIKit or Charts
      Model/                   wire values decoded from the API
      Derived/                 every decision a screen makes, as a function of values
    Net/
      APIClient.swift          one client: base URL, bearer token, decode, APIError
      Endpoints/<Thing>API.swift
    Health/                    the one door into HealthKit: HealthLibrary, one reader per kind of data
    Auth/                      AuthStore, Keychain token storage, the refresh gate
    Store/
      ActiveWorkout/           the in-flight workout, disk-backed
    Features/<Area>/           screens and their store
    Design/                    only what stock SwiftUI has no answer for
      Brand/                   Tempo's mark, wordmark, lockup and the views drawn from them
    Support/                   formatters, small utilities
  LionheartTests/
```

- **`Core/`** — decoded models, derived state, load-state enums, formatting. This is where unit tests
  live, and the only layer with tests worth writing in bulk.
- **`Net/Endpoints/<Thing>API.swift`** — `struct TrainingAPI { let client: APIClient }`, one method
  per route in `docs/api-surface.md`. Response types decode in a hand-written `init(from:)` using
  `decodeLenient`, so a member that drifts costs that member and not the screen. Never add auth
  headers: the bearer token and refresh are `APIClient`'s job. Restate `APIError` into the feature's
  own vocabulary here, once.
- **`Features/<Area>/`** — screens and their stores. A store is `@Observable @MainActor final class`,
  `static let shared` only when more than one screen reads it. IO in the store; decisions are pure
  functions in `Core/Derived/`. Load state is an enum whose refresh never drops a loaded value on a
  failed reload. Anything scoped to a session is reset in `AuthStore.signOut`.
- **`Store/ActiveWorkout/`** — the one piece of state the device owns. See "The active workout" below.
- **`Design/`** — see "Apple's styles are the design system".

## The five tabs, and the workout bar

`RootView` is a `TabView` with five `Tab`s, chosen so each answers one question the athlete asks:

| Tab | Question | Screens |
|---|---|---|
| **Today** | what should I do, and how am I? | readiness, last session, next session, quick log |
| **Train** | let me do it | programs, planned sessions, start a workout, the exercise library |
| **History** | what have I done? | the session log, records, trends |
| **Chat** | what does it mean? | the continuing thread and what it remembers, standalone conversations, and the athlete context card the model draws on |
| **Body** | what is my body doing? | Oura days, wellness entries, injuries |

Settings sits behind a toolbar button on Today, not in a sixth tab.

**The active workout is the app's now-playing bar.** While a session is active it shows as a
persistent accessory above the tab bar — elapsed time, the current movement, the rest clock — and
taps up into the logging surface, presented as a sheet. It stays visible across all five tabs, exactly as
Apple Music keeps the track you are playing. On iOS 26 that is `tabViewBottomAccessory`; the iOS 18
path is a `safeAreaInset(edge: .bottom)` bar on the `TabView`. Both render the same
`ActiveWorkoutBar`, so there is one appearance and one fallback, not two designs.

## Apple's styles are the design system

The owner asked for an app that feels native, "like Apple Music", and explicitly does not want the
web app's custom look carried over. So **do not build a design system.** Use Apple's, and let
`Design/` hold only what stock SwiftUI has no answer for.

- **Colour comes from the system.** `Color(.systemGroupedBackground)`,
  `.secondarySystemGroupedBackground`, `.label`, `.secondaryLabel`, `.separator`, and the semantic
  `.red`/`.green`/`.orange` for state. The brand is Tempo's palette in the asset catalog, read through
  `Tokens`: Brick is the accent, applied once as `.tint` in `RootView`; Ink, Iron and Graphite are for
  the mark, a chart's second series and the brand's own grounds. Accents and grounds, never a reskin
  of ordinary controls. Light and dark are then correct for free, which is the whole reason for the
  rule. Never a hex literal in a view.
- **Type comes from the system.** `.largeTitle`, `.headline`, `.body`, `.caption`, with `.bold()`
  where emphasis is wanted. No custom typeface, no tracking or letter-spacing, no gradient text, no
    all-caps section labels — except `Wordmark`, the name drawn as a picture. The brand's two voices
  are the one other exception, and both are system fonts at text styles in
  `Design/Brand/BrandType.swift`: `.heroFigure()` for a hero card's figure (heavy, expanded width)
  and `.meta` for the small label saying what it covers (caption, monospaced). Use them only
  through those modifiers, never as ad hoc fonts. Figures that change in place — a rest clock, a running volume total, a
  weight — carry `.monospacedDigit()` so they do not jitter. Every size is a text style, so Dynamic
  Type works without a `relativeTo:` anywhere.
- **Stock containers, always**: `NavigationStack`, `TabView`/`Tab`, `List`, `Form`/`Section`,
  `LazyVGrid`, `.sheet(item:)`, `.fullScreenCover`, `.searchable`, `.refreshable`,
  `.swipeActions`, `.contextMenu`, `Menu`, `Picker`, `Stepper`, `Gauge`, `ProgressView`,
  `ContentUnavailableView`, `.confirmationDialog`, `.alert`, and `Charts` for trends. Build a custom
  control only when no stock one does the job, and say why in its doc comment.
- **`List` with `.listStyle(.insetGrouped)` is the default screen.** It is what Apple Music, Fitness
  and Settings are made of, it gets separators, swipe actions, selection and Dynamic Type right, and
  it is the fastest way to a screen that looks like it belongs.
- `Design/` therefore holds: `Tokens.swift` (the palette and the handful of spacing constants),
    `Brand/` (`ProgressionMark`, the mark's one `Shape`, and `BrandMark`, `Wordmark`, `BrandLockup`,
  `MarkLoadingIndicator`, `LeanedBar`, `LeaningBars`, `BrandType`, `HeroSection`, `HeroFigure`),
  `StatTile.swift`, `SetRow.swift`, `RestTimerBar.swift`, `ActiveWorkoutBar.swift`, and
  `Sparkline.swift`, `MovementLines.swift`, `SummaryFigures.swift`, plus the shared chrome and load-state rows. If another file wants in, check
  first whether SwiftUI already has it.
- **Haptics on the actions that matter** — completing a set, finishing a workout, hitting a record —
  through `Support/Haptics.swift`. A confirmation is a haptic plus the state visibly changing, never
  a toast for something the screen already shows.

## The server's contract

- **Transitions, not shapes.** There is no `PUT /sessions/{id}`. A session moves with
  `POST /{id}/start`, `/complete`, `/skip`; its sets are replaced whole with `PUT /{id}/sets`. A
  transition from the wrong state answers 409 — a domain refusal the UI states, never an error to
  swallow.
- **Problems arrive as RFC 7807.** `ResultToHttp` maps the server's `Problem` cases to 400 (with
  `ValidationProblemDetails.errors` keyed by field), 401, 403, 404, 409, 502, 500. `APIError` decodes
  `detail` for a message and `errors` for field problems; a 404 means *not found or not yours* and
  reads as not found.
- **Polymorphic bodies carry `$type`.** Sets, periods, effort, decoration and the interpretation
  outcomes are sealed hierarchies serialized with a `$type` discriminator. Decode them through a
  total `init(wire:)` with an unrecognised case — never a `default` that drops the row.
- **`AwaitingConfirmation` is a success.** The interpretation routes answer 200 with a discriminated
  body; the client echoes the parsed draft plus the user's choices back to `POST /confirm`, and the
  server re-resolves without re-parsing. Treating it as an error is the mistake this line prevents.
- **Auth is ASP.NET Identity bearer tokens.** `POST /login` with `{ email, password }` and no
  `useCookies` returns `accessToken`, `refreshToken`, `expiresIn`; `POST /refresh` renews. Tokens
  live in the Keychain, never `UserDefaults`. `APIClient` attaches the bearer, and a single actor
  gates refresh so a burst of 401s produces one refresh, not five.

## The active workout

The only state the device owns, because the gym is where the signal is worst and a lost set is the
one failure that would make the owner stop using the app.

- `ActiveWorkoutStore` holds the session id, the ordered sets logged so far, the rest clock and the
  current movement. Every mutation writes the whole value to a file in Application Support — a small
  JSON document, rewritten atomically — so a crash or a force-quit loses nothing.
- It pushes to the server with `PUT /{id}/sets` **after** persisting locally, coalesced so rapid
  logging produces one request, and it is not an error for that push to fail: the bar shows an
  unsynced state and the next push carries everything.
- Finishing calls `POST /{id}/complete` with the period and the effort ratings. Only a 2xx, or the
  finished workout safely in the outbox when the server cannot be reached, clears the local
  document. A workout started with no connection is a deferred start, opened on the server by its
  first push that gets through; `docs/ios-architecture.md` → "Starting and finishing with no
  connection" has the rules.
- The rest clock is a stored `Task` in the store, not a `Timer`, and the *view* reads it through
  `TimelineView(.periodic)` so the tick redraws the clock and not the set list.
- Its rules — next set from the prescription, running volume, whether this set beat a record — are
  pure functions in `Core/Derived/ActiveWorkout/`, so they are unit-tested without a host.

## Web → SwiftUI

The web app is a source of *behaviour*, never of appearance.

| Web (SvelteKit, Svelte 5) | SwiftUI here |
|---|---|
| A route under `ClientApp/src/routes/` | A case of the area's `Route` enum, registered once with `.navigationDestination(for:)`. One destination per value type — register two for the same type and the later silently wins. |
| `$state` | A stored property on an `@Observable` store, or `@State` for view-local UI state |
| `$derived` | A computed property, with the logic a pure function in `Core/Derived/` |
| `$props` | `let` inputs; `@Binding` only when the child writes back |
| A store in `ClientApp/src/lib/stores/` | A `Features/<Area>/…Store.swift`, same responsibilities, load state as an enum |
| `ClientApp/src/lib/api/training.ts` | `Net/Endpoints/TrainingAPI.swift`, method for method |
| A modal under `components/modals/` | `.sheet(item:)` hung on the screen, not on each row; the sheet owns its `NavigationStack` and toolbar, `.presentationDetents` where it is short |
| A card grid | `LazyVGrid(columns: [GridItem(.adaptive(minimum: …))])` in a `ScrollView`, or a `List` if it is really rows |
| Form and inputs | `Form` + `Section` + stock controls |
| Dropdown | `Menu`; an option set inside one is a `Picker` |
| In-page tabs | `Picker(…).pickerStyle(.segmented)` |
| Tailwind utility classes | Nothing. The system styles above |
| `localStorage` preference | `@AppStorage("lh.<name>")` |
| Hover-only affordance | `.contextMenu` and/or `.swipeActions` |
| An error page | The error case of the load-state enum, drawn with `ContentUnavailableView` and a retry |
| Search box | `.searchable(text:)`, with `.searchScopes` where the web has scopes |
| Nothing (the web has no pull to refresh) | `.refreshable { await store.refresh() }` |

## Green

From the worktree root. Every agent uses its own simulator clone and its own derived-data path — two
builds sharing either corrupt each other.

```bash
cd ios && xcodegen generate
SRC=$(xcrun simctl list devices available | grep -m1 "iPhone 17 (" | grep -oE '[0-9A-F-]{36}')
UDID=$(xcrun simctl clone "$SRC" "lh-<your-branch>")
xcrun simctl boot "$UDID" && xcrun simctl bootstatus "$UDID" -b
xcodebuild -project Lionheart.xcodeproj -scheme Lionheart \
  -destination "platform=iOS Simulator,id=$UDID" \
  -derivedDataPath "$PWD/DerivedData" test
./Scripts/check-layering.sh
```

- While iterating add `-only-testing:LionheartTests/<Class>`; run the whole suite on the final tree.
- `check-layering.sh` compiles nothing; the `xcodebuild` run is what proves the Swift.
- Tests never touch the network. Fixtures are inline JSON held beside the test.
- The backend's own green is `dotnet build` plus `dotnet test lionheart.sln` from the repo root — run
  it when a change touches both sides, since the wire types are the contract between them.
- When done: `xcrun simctl shutdown "$UDID"; xcrun simctl delete "$UDID"`. Stop only what you
  started — never `pkill` by pattern.

## Looks right

A screen is not done until it has been seen running.

1. Start the backend: `dotnet run --launch-profile lionheart` from the repo root serves
   `http://localhost:7025` — the profile is named after the project, and there is no `http` profile.
   A fresh database has no schema, so `dotnet ef database update` comes first, and an account needs
   two calls (`/register` then `/api/user/create-profile`) because a login with no Lionheart profile
   is refused by every training route. The simulator reaches the host's localhost directly; a physical phone
   needs the Mac's LAN address or a tunnel, set in `ios/Configs/Local.xcconfig`.
2. Build, `xcrun simctl install "$UDID" <app>`, then
   `xcrun simctl launch --terminate-running-process "$UDID" com.chasebalmer.lionheart`.
3. `xcrun simctl io "$UDID" screenshot <file>.png` and look at it. Screenshots stay out of the repo.
4. Repeat with `xcrun simctl ui "$UDID" appearance dark` and with
   `xcrun simctl ui "$UDID" content_size accessibility-large`: nothing clips, no figure truncates,
   no control is lost.
5. Say in the PR which states were seen and which could not be — "no Oura data on this database for
   the empty-readiness case" is a useful sentence; silence is not.
