---
name: swift-engineering-standards
description: House engineering standards for Swift and SwiftUI in ios/, plus the design-before-code survey for any nontrivial change to the native client. Governs domain-state modeling with enums, exhaustive switches, failure as a value (throws, typed throws, Result), value types and immutability, pure Core/ functions behind thin @Observable @MainActor stores, Swift 6 strict concurrency under this project's actual build settings, names and folders, comments, and simplicity. Use before writing or modifying any Swift or SwiftUI code under ios/, even when the user never says "standards" — consult it before implementation starts, not only at review. Load it alongside the lionheart-ios skill, which owns where things go in this app, the API-to-SwiftUI translation, and what green means; this skill sets how the Swift is written.
---

# Swift Engineering Standards

Every task is a **production-grade fix or enhancement** by default, never a quick patch, unless the
user says otherwise. Readable, deliberately designed, and bulletproof while staying concise. Concise
and bulletproof, not clever and defensive.

These are the house principles of the C# `engineering-standards` skill, written for Swift. Where
Swift's type system already does what C# needs a convention for (exhaustive `switch`, value types,
`throws` as the failure track), use the language and do not rebuild the C# machinery on top of it.

| Topic | File |
|---|---|
| Swift shapes: the ADT, the load-state enum, the three failure channels, concurrency patterns | `references/swift-idioms.md` |
| When a comment earns its keep; `///` doc comments | `references/comments.md` |

## The project's configuration — write for this, not for a template

Read `ios/project.yml` before trusting any concurrency advice, including Apple's. As of this writing:

- **Swift 6 language mode, `SWIFT_STRICT_CONCURRENCY: complete`**, iOS **18.0** deployment target.
- **No default actor isolation** (`SWIFT_DEFAULT_ACTOR_ISOLATION` is unset), so every declaration is
  nonisolated unless it says otherwise. The "main actor by default" mode in Apple's Swift 6.2
  concurrency guide is opt-in and **not** on here.
- **`NonisolatedNonsendingByDefault` is off.** A nonisolated `async` function therefore leaves the
  caller's actor and runs on the concurrent pool — the opposite of what Apple's 6.2 guide describes
  for an "approachable concurrency" project. Passing a non-`Sendable` value into one from the main
  actor is a "sending … risks causing data races" error, not a quirk.
- Changing any of these is the owner's decision. Never flip one as a side effect of a change.
- An API introduced after iOS 18 sits behind `if #available(iOS 26.0, *)` with an iOS 18 path. The
  simulators on this machine run iOS 26+, so the compiler will **not** warn about an iOS 26-only API
  — the availability check is the only thing that keeps the iOS 18 floor honest.

## Before writing code — the design survey

Work through these before anything nontrivial; the order carries no meaning.

### Establish where the state lives
Lionheart's truth is the server. The device holds three kinds of state and they are not
interchangeable:

- **Server-owned, fetched per screen** — sessions, contexts, programs, Oura days, injuries. It lives
  in the screen's store as a load-state enum, and the server is re-read rather than reconciled.
- **The active workout** — the one piece of state the device owns while it is in flight, because a
  set logged in a basement with no signal must not be lost. It lives in `Store/ActiveWorkout/`,
  persists to disk on every mutation, and reconciles to the server through the session's
  `PUT /sets` replace-whole route.
- **Settled per-user facts** — the signed-in identity and its tokens (`Auth/`), and view preferences
  (`@AppStorage`).

Decide which of the three before writing; the wrong one is not fixed by a later refactor.

### Find the sibling — for conventions, not for standards
Take the closest existing implementation's **packaging** — folder, file naming, how a feature splits
into screen, store and Core derivation — never its design. A sibling that predates or breaks a rule
here is not a licence: write the better version and say in your summary what you changed and why.
No count of existing sites using a prohibited pattern outweighs the rule.

### Enumerate domain states as domain facts
List every state the data can be in **according to the domain**, not according to today's handlers.
Two states handled identically today are still two states. Model a closed set as an `enum` with
associated values, so an invalid combination cannot be constructed. If you believe two states are
one, ask the user.

The backend has already done this work and the client must not undo it. A training session is
`planned | active | completed | skipped` as four sealed server records with different fields — a
`SessionStatus` string plus a bag of optionals on one Swift struct is the defect this rule exists to
prevent. Mirror the hierarchy as an `enum` with associated values; `Core/Model/TrainingSession.swift`
is the reference.

### Do a reuse pass
- **Does this logic exist?** Search `Core/` first, then the store that owns the data. Re-implementing
  a derivation that already lives in `Core/Derived/` is a defect.
- **Is this the second call site?** Extract at the second consumer, every time.
- **Does the server already derive it?** Volume, pace, PR status and the athlete context card are
  the server's answers. Re-deriving one on the device gives two numbers that disagree. Port a rule
  only when the device needs it offline (the active workout's running volume and rest timer), and
  say in the doc comment which server rule it mirrors.

### Name new data clusters
Fields that arrive together and describe one concept are a new `struct`, not loose properties on the
store or the snapshot. The test: does the cluster have a name in the domain? Words come from
`docs/glossary.md`; a noun you coin gets its line there in the same change.

### One job per unit
A store method that fetches, decides and presents is three things: the IO in the store, the decision
in a pure `Core/` function, the presentation in the view. Name the steps before writing them.

## Compose, don't complicate

- **`try` is the railway.** A throwing orchestrator is a sequence of `try` steps that short-circuits
  on the first failure, read top to bottom as the story of what happens. The edge — the store
  method a view calls — ends it in one `do`/`catch` that turns the outcome into state. Nothing between
  the first step and the edge inspects an error to decide what to do next.
- **Restate at a boundary, once.** A layer that speaks its own failure vocabulary maps the lower
  layer's error in one total function — the Swift form of `MapError`. Never let a view match on
  `APIError.http` status codes; the server's `Problem` vocabulary is restated into the feature's
  failure type at the endpoint type that owns the route.
- **Transform values, don't mutate across a block.** `map`/`filter`/`reduce`/`compactMap`, a
  function that returns a new value, `switch` as an expression. A loop carrying state from one
  iteration to the next is a `reduce`.
- **The extraction criterion is a name.** A function earns existence by naming a domain step
  (`ActiveWorkout.appending(_:)`), never by shortening its parent.

## Core patterns

The shapes behind these rules are in `references/swift-idioms.md`; read it before writing the first
one.

- **Closed state sets are enums with associated values.** Never a `String` status, a `Bool` pair
  encoding three states, or an `isLoading` + `error: String?` + `value: T?` bag. "Not loaded yet" is
  a case, not a `nil`.
- **Exhaustive `switch`, no `default:` over a domain enum you own.** The compiler checks enum
  exhaustiveness; `default` switches that check off, so the case added next year is silently
  absorbed. List the cases, grouped (`case .completed, .skipped: true`).
- **Apple's enums are non-frozen: `@unknown default`, plus every known case.** `@unknown default`
  matches only cases added after you compiled and warns when a known case is unhandled; plain
  `default` hides both. Swift 6 mode rejects a switch over `ScenePhase` with no default at all.
- **Wire values decode totally.** A string from the server becomes an enum through a total
  `init(wire:)` with an `.unrecognised(String)` case — one malformed value must never make a list
  unloadable. The server's polymorphic payloads carry a `$type` discriminator; an unrecognised
  discriminator decodes to the hierarchy's unknown case and the row still renders. The `default:`
  inside that initialiser is the one legitimate `default` over strings; everywhere else switches on
  the enum.
- **Value types by default.** `struct` and `enum` for domain data, `let` for every property that does
  not change, a new value instead of mutation where the value is shared. A `class` needs a reason:
  identity (a store, a connection) or Objective-C interop (a delegate). A class is `final` unless
  designed for subclassing.
- **Failure is a value — pick the channel by what happens to it.** Plain `throws` by default, and
  always where cancellation or a foreign error can flow; `throws(E)` only where the failure set is
  closed, owned, and every caller must handle each case; `Result<T, E>` only where a failure is
  *stored or passed* rather than propagated — folded into a state enum, carried across a callback.
  The full rule and its reasons are in `references/swift-idioms.md` → "Failure as a value".
- **Cancellation is never a domain failure.** It propagates as `CancellationError`; a `catch` that
  maps "everything else" into a domain case must let it through first. `try?` is for failures that
  genuinely carry no information, never for an `APIError`.
- **Presence without `nil` where absence means something.** An optional is right for "this field may
  be empty"; it is wrong for a state the UI renders differently. Then it is a case. The server models
  the same distinction (`Rpe.Unrated`, `EffortNotReported`, `NotDecorated`) — decode those to cases,
  never to `nil`.
- **Totality at boundaries.** A function over trusted input returns a value, not an optional or a
  throw. Validate once at the wire, and pass the validated type inward.

## Stores, views and `Core/`

- **`Core/` is pure values and functions** — no `SwiftUI`, `UIKit` or `Charts` import, enforced by
  `ios/Scripts/check-layering.sh`. Every decision a screen makes — what a readiness ring says, which
  set is next, whether a lift beat a record — lives here as a function of values, so it is unit-tested
  without a host. Namespaces are caseless `enum`s or static functions on the value type.
- **A store is `@Observable @MainActor final class` and does IO and coalescing, nothing else.** It
  exposes `private(set)` state, decides nothing it could ask `Core/`, and turns each outcome into a
  state case at one `do`/`catch`.
- **Views hold `@Observable` objects the Observation way.** `@State` in the view that creates the
  object, a plain `let`/`var` where a view only reads it, `@Bindable` where it needs bindings,
  `@Environment(Type.self)` for one an ancestor supplies. Not `ObservableObject`, `@Published`,
  `@StateObject`, `@ObservedObject` or `@EnvironmentObject` in new code.
- **SwiftUI re-renders on what `body` reads.** Read the narrowest property that answers the question;
  hand a subview the value it needs, not the whole store, when it needs one field. This matters most
  in the active workout, where a one-second rest timer tick must not redraw the set list.
- **`@State` is `private`, local, and initialised at its declaration.** Apple: declaring it private
  prevents a memberwise initialiser setting it, which "can conflict with the storage management that
  SwiftUI provides". Its default is instantiated every time the view is, so an expensive default
  (an object, a parse) is created in `.task` instead.

## Concurrency

Rules for **this** configuration — see "The project's configuration" above.

- **Isolate by declaration, not by hope.** Anything a view reads is `@MainActor`. State the isolation
  that already holds (`@MainActor` on the type, `nonisolated` on a member that touches no actor
  state, `nonisolated static` for helpers called from delegate queues).
- **Offload with `@concurrent`, not `Task.detached { }.value`.** CPU or blocking IO goes in a
  `nonisolated` `@concurrent` `async` function the caller `await`s. It compiles on the iOS 18 target,
  is the default behaviour today, and keeps meaning "off the actor" if `NonisolatedNonsendingByDefault`
  is ever turned on. A detached task is unstructured: no parent, so cancelling the awaiting task does
  not reach it.
- **Structured first.** A fixed number of parallel calls is `async let`; a dynamic number is
  `withTaskGroup`/`withThrowingTaskGroup`. Both create child tasks, so the parent cannot finish
  without them and its cancellation reaches them. The Today screen's four parallel loads are
  `async let`.
- **Work tied to a view's lifetime is `.task` / `.task(id:)`**, which SwiftUI cancels when the view
  disappears and restarts when `id` changes — never `onAppear { Task { } }`.
- **`Task { }` only at a boundary or as an owned handle.** Legitimate: a synchronous event handler
  (a `Button` action, `onChange`, a delegate callback) starting async work; or a stored
  `Task<…, Never>?` a store keeps, cancels and replaces (the rest timer, an in-flight refresh).
  Never a fire-and-forget `Task { }` inside an `async` function — just `await` the work.
- **Cancellation is cooperative.** A long loop checks `Task.isCancelled` or calls
  `try Task.checkCancellation()`; a sleep is `try await Task.sleep(for:)` followed by a cancellation
  check.
- **`Sendable` is a claim the compiler checks; keep it checkable.** Value types with `Sendable`
  members are `Sendable` — say so on `Core/` types that cross actors (`Equatable, Sendable`). A
  `final class` is `Sendable` only with immutable `Sendable` stored properties (`Net/APIClient`).
  An `actor` owns mutable state reached asynchronously (the token refresh gate).
- **`@unchecked Sendable` and `nonisolated(unsafe)` carry a doc comment naming the lock or queue**
  that makes them true. New locking uses `Mutex` from `Synchronization` (iOS 18) rather than `NSLock`.
- **A main-actor type conforming to a nonisolated protocol** uses an isolated conformance,
  `extension Store: @MainActor Protocol`, rather than marking the members `nonisolated` and reaching
  back with `MainActor.assumeIsolated`.

## Common mistakes to avoid

Legacy APIs still compile here, most without a warning (deprecated in iOS 26 or "a future version";
rows marked "not deprecated" are house preferences). Each replacement is available at iOS 18
unless marked.

| Instead of | Write |
|---|---|
| `ObservableObject` + `@Published`, `@StateObject`, `@ObservedObject`, `@EnvironmentObject` | `@Observable` class; `@State`, plain property or `@Bindable`, `@Environment(T.self)` |
| `NavigationView`; `NavigationLink(destination:isActive:)` | `NavigationStack` / `NavigationSplitView`; `NavigationLink(value:)` + `navigationDestination(for:)` |
| `.foregroundColor(_:)` (View and Text) | `.foregroundStyle(_:)` |
| `.cornerRadius(_:)` | `.clipShape(.rect(cornerRadius:))` or a filled shape |
| `.onChange(of:perform:)` with one parameter | `.onChange(of:) { old, new in }` or `{ }` |
| `.animation(_:)` with no value | `.animation(_:value:)` or `withAnimation` |
| `Text(a) + Text(b)` | `Text("\(a)\(b)")` — `Text` interpolates `Text` |
| `.overlay(view, alignment:)`, `.background(view, alignment:)` | `.overlay(alignment:) { }`, `.background(alignment:) { }` |
| `.navigationBarTitle`, `.navigationBarItems`, `.navigationBarHidden` | `.navigationTitle`, `.toolbar { }`, `.toolbarVisibility(.hidden, for:)` |
| `.toolbar(.hidden, for:)` | `.toolbarVisibility(.hidden, for:)` |
| `.navigationBarLeading` / `.navigationBarTrailing` placements | `.topBarLeading` / `.topBarTrailing` (or a semantic placement) |
| `.edgesIgnoringSafeArea` | `.ignoresSafeArea(_:edges:)` |
| `.accentColor(_:)` | `.tint(_:)` or the asset catalog accent |
| `.autocapitalization`, `.disableAutocorrection` | `.textInputAutocapitalization`, `.autocorrectionDisabled` |
| `.actionSheet`, `.alert(isPresented:content:)` returning `Alert` | `.confirmationDialog`, `.alert(_:isPresented:actions:message:)` |
| `.tabItem` | `Tab(_:systemImage:value:content:)` |
| `Task.sleep(nanoseconds:)` (not deprecated; the bare `sleep(_:)` is) | `Task.sleep(for: .seconds(…))` — a `Duration` reads |
| `DispatchQueue.main.async` from Swift code you own (not deprecated) | `@MainActor` isolation and `await` |
| `await Task.detached { … }.value` to offload | a `nonisolated` `@concurrent` `async` function |
| A custom `EnvironmentKey` with `defaultValue` | `@Entry var name: T = default` in an `EnvironmentValues` extension |
| `GeometryReader` to read a size | `onGeometryChange(for:of:action:)`, `containerRelativeFrame` (iOS 17), a layout container |
| `.glassEffect` on several sibling views | one `GlassEffectContainer` around them (iOS 26, behind `#available`) |
| `InlineArray` / `Span` without a check | `#available(iOS 26.0, *)` — both are iOS 26 |
| `switch` over an Apple enum with `default:` | every known case plus `@unknown default` |
| `Timer.scheduledTimer` for the rest clock | a stored `Task` with `Task.sleep(for:)`, or `TimelineView(.periodic)` where only the view needs the tick |

`GeometryReader` is not banned — it is right when a child is positioned from the container's
geometry. It is wrong as a way to read one size.

## Simplicity — and what it does not mean

Remove machinery the problem does not need: a protocol with one conformer and no second coming, a
generic with one instantiation, configuration nobody sets, a view model wrapping a store that
already is one. Judge **types** against the domain's states (keep distinctions even when handled
alike), **seams** against changes known to be coming, and **machinery** against the call sites you
are writing. Building for an imagined future is waste; refusing a future already committed to is a
rewrite you scheduled.

## Names and folders

The path should tell a reader what the file holds before they open it: every folder on the way, then
the file name.

- **Names come from the domain's words, as many as the description needs** — `RestTimerState`,
  `SessionPrescription`. No rare word standing in for a plain description; no abbreviation. Terms
  come from `docs/glossary.md`, and a new noun gets its line there in the same change.
- **Apple's API Design Guidelines govern the grammar**: clarity at the point of use, argument labels
  that read as a phrase, Booleans that read as assertions (`isRated`, `acceptsNewSets`),
  mutating/non-mutating pairs (`sort`/`sorted`, `apply`/`applying`), types and protocols as nouns
  or `-able`/`-ing` capabilities.
- **The top-level folders are fixed by purpose** — `Core/` (pure: `Model/` for wire values,
  `Derived/` for derivations), `Net/`, `Store/`, `Auth/`, `Features/<Feature>/`, `Design/`,
  `Support/`. A new feature is a folder under `Features/` holding its screens and its store; its
  pure rules go in `Core/Derived/`.
- **Folders are earned by groups.** Three files sharing a purpose can earn a folder, five clearly do;
  never a folder for one file. A change that adds the fifth file to a folder that has outgrown its
  shape proposes the new tree in its summary first.
- **One primary type per file, named after it.** Small private helpers stay beside the one type that
  uses them.
- **The project is generated.** A file added, moved or removed needs `cd ios && xcodegen generate`;
  never edit the `.xcodeproj`. Moves are `git mv` in their own commit.
- **A rename is a prose change too.** Read the sentence a renamed word sat in, not just the token.

## Comments

Comments are a liability that must earn their keep; the highest-value one records a *why* the code
cannot. `///` on types and on members whose contract is not obvious from the signature. The bar, the
never-write list and the four-question test are in `references/comments.md`. This repo additionally
forbids narrating the model a type replaced — describe the current design on its own terms.

## After writing

- Green is defined in the **lionheart-ios** skill: `xcodegen generate`, the full `xcodebuild … test`
  on your own simulator clone, and `ios/Scripts/check-layering.sh`.
- Behaviour this change adds or alters needs a test in `LionheartTests/`; `Core/` logic is where the
  tests belong. The **test-design** skill governs what earns one.

## Sources

Bundled with Xcode (`/Applications/Xcode.app/Contents/PlugIns/IDEIntelligenceChat.framework/Versions/A/Resources/AdditionalDocumentation/`):
- `Swift-Concurrency-Updates.md` — default main-actor mode is opt-in; `@concurrent` for offloading;
  isolated conformances; `@MainActor` on a class protecting its state.
- `SwiftUI-Implementing-Liquid-Glass-Design.md` — `GlassEffectContainer` for multiple glass views.
- `Swift-InlineArray-Span.md` — what the types are for.
- `SwiftUI-Styled-Text-Editing.md` — `foregroundStyle` preferred over `foregroundColor`.

Apple online:
- Choosing Between Structures and Classes — https://developer.apple.com/documentation/swift/choosing-between-structures-and-classes
- Sendable — https://developer.apple.com/documentation/swift/sendable
- Mutex — https://developer.apple.com/documentation/synchronization/mutex (iOS 18)
- Migrating from ObservableObject to Observable — https://developer.apple.com/documentation/swiftui/migrating-from-the-observable-object-protocol-to-the-observable-macro
- Observation — https://developer.apple.com/documentation/observation
- State — https://developer.apple.com/documentation/swiftui/state
- `task(id:…)` — https://developer.apple.com/documentation/swiftui/view/task(id:name:priority:file:line:_:)
- Writing symbol documentation in your source files — https://developer.apple.com/documentation/xcode/writing-symbol-documentation-in-your-source-files
- API Design Guidelines — https://www.swift.org/documentation/api-design-guidelines/

The Swift Programming Language (github.com/swiftlang/swift-book):
- Enumerations — switch exhaustiveness, associated values.
- Statements → Switching Over Future Enumeration Cases — `@unknown default`.
- Error Handling → Specifying the Error Type — when to type a throw.
- Concurrency — structured vs unstructured tasks, `async let`, task groups, cooperative
  cancellation, `Task.sleep(for:)`, `Sendable`.

Swift Evolution:
- SE-0413 Typed throws — "When to use typed throws".
- SE-0461 Run nonisolated async functions on the caller's actor by default — `@concurrent` is the
  current default; `NonisolatedNonsendingByDefault` is the flag.

Codebase: `ios/project.yml`, `ios/Scripts/check-layering.sh`, `docs/ios-architecture.md`,
`docs/api-surface.md`, `docs/glossary.md`.
