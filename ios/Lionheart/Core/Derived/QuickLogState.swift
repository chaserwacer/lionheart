import Foundation

/// Where a quick-log entry stands.
///
/// `.needsConfirmation` is a **success** case sitting beside `.committed`, not next to `.refused` —
/// the interpretation engine answers 200 for it, and modelling it as a failure is the specific
/// mistake the server's own documentation warns about. The screen that collects the athlete's
/// choices and resumes through `POST /confirm` does not exist yet, so this foundation names the
/// state honestly and says so; it does not pretend the entry was logged.
enum QuickLogState: Equatable, Sendable {
    case idle
    case interpreting
    case committed(sessions: Int, contexts: Int, warnings: [String])
    case needsConfirmation([UnresolvedReference])
    case refused(LoadFailure)

    var isInterpreting: Bool {
        switch self {
        case .interpreting: true
        case .idle, .committed, .needsConfirmation, .refused: false
        }
    }

    /// Whether there is an outcome worth showing the athlete.
    var hasOutcome: Bool {
        switch self {
        case .committed, .needsConfirmation, .refused: true
        case .idle, .interpreting: false
        }
    }

    static func after(_ outcome: InterpretationOutcome) -> QuickLogState {
        switch outcome {
        case .committed(let committed):
            return .committed(
                sessions: committed.sessionIds.count,
                contexts: committed.contextIds.count,
                warnings: committed.warnings)
        case .awaitingConfirmation(let awaiting):
            return .needsConfirmation(awaiting.references)
        case .unrecognised(let type):
            // A discriminator this client has not heard of. The entry may well have been written,
            // so saying "it failed" would be wrong — say that the answer was not understood.
            return .refused(.unavailable(
                "Tempo did not understand the server's answer (\(type.isEmpty ? "no type" : type))."))
        }
    }
}
