# Swift — the shapes behind the rules

The goal is elegance the reader experiences, not cleverness the author enjoys. The complete snippets
here (the typed-throws parse, the boundary restatement, `@concurrent`, `async let`, `Mutex`) were
type-checked with Swift 6 at `arm64-apple-ios18.0` under this project's settings (Xcode 27.1,
2026-10-19); snippets with `…` are shapes. Re-check against `ios/project.yml` if those settings move.

- [Closed state as an enum](#closed-state-as-an-enum)
- [Wire values decode totally](#wire-values-decode-totally)
- [The load-state enum](#the-load-state-enum)
- [Failure as a value](#failure-as-a-value)
- [The orchestrator](#the-orchestrator)
- [Concurrency shapes](#concurrency-shapes)
- [Values, not mutation](#values-not-mutation)

## Closed state as an enum

A closed set of domain states is an `enum`, each case carrying exactly the data that state has:

```swift
enum RowStatus: Equatable, Sendable {      // abridged from Core/Derived/RowStatus.swift
    case busy(String)
    case pipeline(String)
    case transcribing
    case problems(label: String, count: Int)
    case noMatch
    case validated
    case readyToPublish
    case needs(String)
    case none
}
```

`count` exists only on `.problems`, so "a count with no problems" cannot be written. Match with
`switch` — as an expression where it returns a value — and list every case:

```swift
var isBusy: Bool {
    switch self {
    case .busy, .pipeline, .transcribing: true
    case .problems, .noMatch, .validated, .readyToPublish, .needs, .none: false
    }
}
```

The compiler proves this exhaustive, which is what C# needs a discard arm and a throw to
approximate. `default:` gives that proof away: the case added next year compiles straight into
whichever answer `default` returns. The same trap wears a second costume — `case .published: x;
default: y` reads as deliberate handling and absorbs a new case exactly as `default` does.

**Apple's enums are not yours.** A non-frozen enum from a framework (`ScenePhase`,
`AVAuthorizationStatus`) can gain cases after you ship, so a switch over one needs a default. Write
`@unknown default`, never `default`: it matches only cases unknown at compile time and warns when a
known case is missing, so the next SDK's case still reaches you.

```swift
switch phase {
case .active: resume()
case .background: suspend()
case .inactive: break
@unknown default: break
}
```

**Orthogonal states wrap; they don't multiply.** Deletion is orthogonal to `Stage`
(`Core/Model/Stage.swift` says so) — model it as its own fact beside the stage, not as
`.deletedCaptured`, `.deletedQueued`, … Two independent facts across three cases each beat one flat
set of nine.

## Wire values decode totally

A value from the server is decoded through a **total** initialiser: every string produces a case.
One unrecognised value must not make the row — or the list — unloadable.

```swift
enum EnrollmentStatus: Equatable, Sendable {
    case notStarted, deferred, inProgress, completed
    case unrecognised(String)

    init(wire: String?) {
        switch wire {
        case "notStarted": self = .notStarted
        case "deferred": self = .deferred
        case "inProgress": self = .inProgress
        case "completed": self = .completed
        default: self = .unrecognised(wire ?? "")
        }
    }
}
```

This `default:` is the one legitimate one over strings: it is where the open set (the wire) becomes
the closed one. Everything past the initialiser switches on the enum. The decision about what an
unrecognised value *means* — refuse, show, treat as the safest case — is made where it is consumed,
exhaustively (`Stage.acceptsNewUploads` refuses `.unknown`).

A string that is matched on in more than one place (`ListingProblem.stage`, `PendingEdit.kind`) has
already become a domain value and should be decoded into an enum once.

## The load-state enum

Anything a view shows after asking for it has at least three states, and the house shape names them
(`EnrollmentSnapshot`, `Core/Derived/EnrollmentProgress.swift`):

```swift
enum EstimateState: Equatable, Sendable {
    case loading
    case unavailable(EstimateFetchFailure)
    case loaded(Estimate)
}
```

Not `@State var loading = true; var error: String?; var estimate: Estimate?` — eight combinations,
five of them nonsense, and every view branch has to rediscover which three are real. The domain
rules about moving between states are a pure function on the enum, in `Core/`:

```swift
/// A failed refresh never discards a value already shown.
func applying(_ result: Result<Estimate, EstimateFetchFailure>) -> EstimateState
```

Add a state the domain has — `.stale(Estimate)`, `.refreshing(Estimate)` — rather than a Boolean
beside the enum.

## Failure as a value

### What the codebase does today

- `APIError` (`Net/APIError.swift`) is a closed enum with a total `isTransient` classification.
- `APIClient`'s methods are plain `throws`. They throw `APIError`, but also `CancellationError` (from
  `URLError.cancelled`, deliberately) and `EncodingError` (from `JSONEncoder` in `post`/`put`). **The
  failure set is not closed**, so plain `throws` is correct there.
- Callers match patterns, most specific first, with a final catch-all:
  `catch APIError.http(let status, _, _) where status == 404` … `catch let error as APIError where
  error.isTransient` … `catch` (`Store/EditQueue.swift`, `Store/MirrorObserver.swift`).
- A layer restates `APIError` in its own vocabulary through one total function
  (`EbayFailure.from(_:)`, `Net/Endpoints/EbayAPI.swift`).
- `Result` appears where a failure is **data**: folded into a state enum
  (`EnrollmentStore.reload` → `EnrollmentSnapshot.applying(_:)`) and passed through a completion
  handler (`Auth/NativeAuth.swift`).
- No `throws(E)` yet.

### The rule

| The failure… | Use | Why |
|---|---|---|
| propagates, and cancellation, a system error or a lower layer's error can be among it | plain `throws` | The default. Most async IO is here: `Task.sleep`, `Task.checkCancellation` and URLSession cancellation throw `CancellationError`, which cannot pass through `throws(E)` unless `E` has a case for it. |
| is a closed set this module owns, every caller must handle each case, and nothing else can be thrown | `throws(E)` | `catch` binds `error` as `E`, a `switch` over it is exhaustive, and adding a case breaks every caller at compile time — the same guarantee as a domain enum. Typically a synchronous `Core/` parse or validation. |
| is passed through generically (a combinator over a caller's closure) | `throws(E)` with generic `E` | Preserves the caller's error type instead of erasing it; the shape of `rethrows`. |
| becomes data — stored in state, folded, sent across a callback, collected per item of a batch | `Result<T, E>` | A thrown error cannot be stored; a `Result` can be. Convert at the edge where it stops travelling. |
| carries no information anyone acts on (a cache miss) | `Optional` / `try?` | Never for an `APIError`, a write, or anything a user or a log should hear about. |

Two refusals from Swift's own guidance: **don't type a throw just because the implementation
happens to throw one kind of error today** (SE-0413), and remember that most Swift code does not
specify its error type (The Swift Programming Language, Error Handling). Typed throws is for a closed
set you will hold closed.

```swift
enum PriceRefusal: Error, Equatable {
    case empty
    case notANumber(String)
    case belowFloor(Money)
}

enum PriceParse {
    static func parse(_ text: String, floor: Money) throws(PriceRefusal) -> Money { … }
}

// The caller handles every case; no catch-all, no cast.
do {
    return .accepted(try PriceParse.parse(text, floor: floor))
} catch {
    switch error {
    case .empty: return .prompt
    case .notANumber(let text): return .refused("\(text) is not a price")
    case .belowFloor(let floor): return .refused("At least \(floor)")
    }
}
```

**Trap — closures do not infer a typed throw.** `Result { try PriceParse.parse(…) }` fails to
compile ("invalid conversion of thrown error type 'any Error' to 'PriceRefusal'"); write the
closure's type: `Result { () throws(PriceRefusal) in try PriceParse.parse(…) }`.

**`Result` is not a replacement for `throws`.** An `async` function returning `Result` so that it
need not throw hand-rolls the railway the language already has, and makes every caller unwrap. Return
`Result` from a function only when its caller's job is to store or forward the outcome.

### Restating at a boundary

The Swift form of `MapError` is one total function from the lower layer's error to this layer's
failure enum. It must let cancellation through before it classifies anything, or a dismissed screen
becomes a user-facing failure:

```swift
static func from(_ error: any Error) throws(CancellationError) -> EbayFailure {
    if let cancelled = error as? CancellationError { throw cancelled }
    guard let api = error as? APIError else { return .other(String(describing: error)) }
    switch api { … }   // exhaustive over APIError
}
```

Map failures to presentation (copy, a banner, whether to retry) once, in the store or the view —
never inside `Net/`.

## The orchestrator

A throwing orchestrator is a sequence of `try` steps — the first failure ends it — and the store at
the edge turns the outcome into state with one `do`/`catch`:

```swift
@Observable @MainActor
final class PricingStore {
    private(set) var state: EstimateState = .loading

    func load(for match: GcdMatch, band: GradeBand) async {
        do {
            let estimate = try await api.estimate(match: match, band: band)
            state = state.applying(.success(estimate))
        } catch is CancellationError {
            return                       // the view went away; nothing to say
        } catch APIError.unauthenticated {
            state = state.applying(.failure(.unauthenticated))
        } catch {
            state = state.applying(.failure(.unavailable(String(describing: error))))
        }
    }
}
```

The steps between the first `try` and the edge never inspect an error to decide what to do next;
the decisions about the outcome live in `applying(_:)`, which is pure and in `Core/`.

## Concurrency shapes

**Offload CPU or blocking work** — a `nonisolated` `@concurrent` function, awaited:

```swift
nonisolated enum Thumbnailing {
    @concurrent static func downsample(_ file: URL, maxEdge: Int) async -> CGImage? { … }
}

let thumb = await Thumbnailing.downsample(file, maxEdge: 240)   // from the main actor
```

Its arguments and result must be `Sendable`. Under this project's settings a plain `nonisolated
async` function already runs off the caller's actor; `@concurrent` states that, and keeps stating it
if `NonisolatedNonsendingByDefault` is enabled, when a plain one would start running on the caller's
actor instead.

**Parallel calls, known count** — `async let`, which `APIClient` being `Sendable` exists to allow:

```swift
async let listing = listings.get(id)
async let enrollment = ebay.enrollment()
let (l, e) = try await (listing, enrollment)
```

**View-lifetime work** — `.task(id:)` with an `Equatable` key built from exactly what the work
depends on (`Features/ListingEdit/PriceHintView.swift`'s `TaskKey`). SwiftUI cancels it when the
view disappears and restarts it when the key changes.

**An owned, coalescing handle** — the one durable use of `Task { }`: a store keeps the handle,
callers await it, and `reset()` cancels it (`Features/Enrollment/EnrollmentStore.swift`).

**Shared mutable state off the main actor** — an `actor` when access is asynchronous; a `Mutex` in a
`final class: Sendable` when callers need a synchronous answer:

```swift
import Synchronization

final class Counter: Sendable {
    private let count = Mutex(0)
    func bump() -> Int { count.withLock { $0 += 1; return $0 } }
}
```

A `Mutex` makes the class checkably `Sendable`; an `NSLock` beside a `var` needs
`@unchecked Sendable` and a promise. Prefer the one the compiler can check.

## Values, not mutation

`struct` with `let` properties for what does not change; a non-mutating method returning a new value
for a state change that is shared or compared (`applying(_:)`); a `mutating` method only on a value
that is local to one owner. Name the pair the API Design Guidelines' way — `apply` mutates,
`applying` returns. A `class` is for identity (a store, a session, a hardware intermediary such as
`AVCaptureSession`) or for Objective-C (a delegate), and is `final`.
