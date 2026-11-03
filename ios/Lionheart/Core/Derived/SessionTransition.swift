import Foundation

/// A transition a session row can offer without being opened first.
///
/// The set a row offers is a question about its lifecycle state, and the server has already
/// answered it: `/start` and `/skip` accept a **planned** session and refuse anything else with a
/// 409, while `DELETE` accepts any state the athlete owns. Offering a transition the state cannot
/// take would be offering an action whose only possible outcome is a refusal, so the rule is
/// mirrored here rather than discovered by tapping — see `offered(on:)` for the two readings that
/// are this client's own.
enum SessionTransition: Equatable, Sendable, Identifiable, CaseIterable {
    case start
    case skip
    case delete

    var id: Self { self }

    var label: String {
        switch self {
        case .start: "Start this session"
        case .skip: "Mark as skipped"
        case .delete: "Delete session"
        }
    }

    var systemImage: String {
        switch self {
        case .start: "play.circle"
        case .skip: "xmark.circle"
        case .delete: "trash"
        }
    }

    /// Whether this is one of the actions iOS draws in red — and, here, the same question as
    /// whether it asks first. Skipping is terminal: the server keeps the session, and there is no
    /// route back to planned.
    var isDestructive: Bool {
        switch self {
        case .start: false
        case .skip, .delete: true
        }
    }

    /// What the athlete is asked before it happens, or `nil` for the one transition that is not
    /// worth a dialog. Starting a session is undone by finishing or discarding the workout.
    var confirmation: Confirmation? {
        switch self {
        case .start:
            nil
        case .skip:
            Confirmation(
                question: "Mark this session as skipped?",
                warning: "A skipped session is kept — the skip is data. It cannot be un-skipped.",
                proceed: "Mark as skipped")
        case .delete:
            Confirmation(
                question: "Delete this session?",
                warning: "The session and every set logged on it are removed. This cannot be undone.",
                proceed: "Delete session")
        }
    }

    /// The transitions a row in this state can offer, in the order a menu should read them.
    ///
    /// Two readings here are the client's and not the server's:
    ///
    /// - **A session in progress offers nothing.** This device may be holding it as the active
    ///   workout, and deleting it underneath that document would strand every set logged since —
    ///   the next push would answer 404 against a session that no longer exists. It is finished or
    ///   discarded from the logging surface, where the document can be cleared with it.
    /// - **A state this build cannot read offers nothing.** The envelope gives it a row, not a
    ///   licence to delete a session whose contents this version does not understand.
    static func offered(on kind: LoggedSessionKind) -> [SessionTransition] {
        switch kind {
        // A planned day gone by is still planned on the server, so starting one late is a
        // transition it accepts — and the common way a missed session is finally trained.
        case .planned, .missed: [.start, .skip, .delete]
        case .completed, .skipped: [.delete]
        case .inProgress, .unrecognised: []
        }
    }
}
