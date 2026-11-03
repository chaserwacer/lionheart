import Foundation

/// Where a one-shot action the athlete asked for stands.
///
/// Three cases rather than a Bool beside an error string: an action is waiting, out, or refused
/// with the server's own words. The third case is the one that earns the type — **a transition
/// asked for from the wrong state answers 409**, and an action that swallows that refusal leaves
/// the athlete believing it worked. A context menu is the easiest place in the app to make that
/// mistake, because nothing on screen changed either way.
///
/// There is no `.succeeded`: a success is the list reloading, which the screen already shows.
enum ActionState: Equatable, Sendable {
    case idle
    case running
    case refused(LoadFailure)

    /// Whether an action is out. Rows disable their own actions on it so one tap cannot start two.
    var isRunning: Bool {
        switch self {
        case .running: true
        case .idle, .refused: false
        }
    }

    /// The refusal to state, if there is one.
    var refusal: LoadFailure? {
        switch self {
        case .idle, .running: nil
        case .refused(let failure): failure
        }
    }

    /// Where an action goes when it throws.
    ///
    /// Cancellation is not a refusal: the only thing that cancels one of these is the screen or the
    /// session going away, and an alert about it would land on a screen that is already leaving.
    static func refusal(from error: any Error) -> ActionState {
        if error is CancellationError { return .idle }
        return .refused((try? LoadFailure.from(error)) ?? .unavailable(String(describing: error)))
    }
}

/// What a destructive action asks before it happens.
///
/// A cluster rather than three strings passed around: the question, the consequence and the word on
/// the button are one piece of writing, and a dialog that asks one thing and warns about another is
/// how an athlete taps through without reading.
struct Confirmation: Equatable, Sendable {
    /// The dialog's title, which is the question being asked.
    let question: String
    /// What happens if they say yes — specifically, what cannot be undone.
    let warning: String
    /// The destructive button's label. Never "OK": the button says what it does.
    let proceed: String
}
