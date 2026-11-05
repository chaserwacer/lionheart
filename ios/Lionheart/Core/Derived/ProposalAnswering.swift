import Foundation

/// Where the athlete's answer to a proposal card stands.
///
/// One answer at a time, named by the card it is for: the card being answered shows its progress and
/// every other card holds its buttons, so two taps can never race to write the same change.
enum ProposalAnswering: Equatable, Sendable {
    case idle
    case answering(UUID)
    case refused(LoadFailure)

    /// The card whose answer is out, if one is.
    var answeringId: UUID? {
        switch self {
        case .answering(let id): id
        case .idle, .refused: nil
        }
    }

    var refusal: LoadFailure? {
        switch self {
        case .refused(let failure): failure
        case .idle, .answering: nil
        }
    }

    /// Whether the refusal means the card no longer says what the server holds — answered on another
    /// device, or gone with its session — so the thread is read again.
    var revealsOutdatedCard: Bool {
        switch refusal {
        case .refused, .notFound: true
        case .offline, .unavailable, nil: false
        }
    }

    /// Where an answer goes when it throws. Cancellation is the screen going away, not a refusal.
    static func refusal(from error: any Error) -> ProposalAnswering {
        if error is CancellationError { return .idle }
        return .refused((try? LoadFailure.from(error)) ?? .unavailable(String(describing: error)))
    }
}
