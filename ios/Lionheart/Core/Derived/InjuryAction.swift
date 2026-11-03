import Foundation

/// What can be asked of an injury from a row.
///
/// `PUT /api/injury/update` carries the whole injury and takes `isActive` with it, so resolving and
/// reopening are one route and two directions — which is exactly why they are two cases here: the
/// row has to say which one it is offering, and an injury cannot be both.
enum InjuryAction: Equatable, Sendable, Identifiable, CaseIterable {
    /// Active → resolved. The injury and its events are kept: a healed problem is the context the
    /// next complaint is read against.
    case resolve
    /// Resolved → active again, for the one that came back.
    case reopen
    case delete

    var id: Self { self }

    var label: String {
        switch self {
        case .resolve: "Mark as resolved"
        case .reopen: "Track again"
        case .delete: "Delete injury"
        }
    }

    var systemImage: String {
        switch self {
        case .resolve: "checkmark.circle"
        case .reopen: "arrow.clockwise.circle"
        case .delete: "trash"
        }
    }

    var isDestructive: Bool {
        switch self {
        case .resolve, .reopen: false
        case .delete: true
        }
    }

    /// Only the delete asks. Resolving is one tap away from being undone by `.reopen`, and a dialog
    /// in front of a reversible action is noise that teaches the athlete to tap through dialogs.
    var confirmation: Confirmation? {
        switch self {
        case .resolve, .reopen:
            nil
        case .delete:
            Confirmation(
                question: "Delete this injury?",
                warning: "Every treatment and check-in logged against it goes too. This cannot be undone.",
                proceed: "Delete injury")
        }
    }

    /// The actions this injury can offer, in the order a menu should read them.
    static func offered(on injury: Injury) -> [InjuryAction] {
        injury.isActive ? [.resolve, .delete] : [.reopen, .delete]
    }
}
