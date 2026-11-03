import Foundation

/// What a lift was for: a working set, a warm-up, a drop set or a set taken to failure.
///
/// Only lifts carry one, as on the server. The distinction that matters most is the warm-up: it
/// sets no record, counts toward no total and answers no target, because it is preparation rather
/// than the work. Drop and failure sets were performed at the weight stated, so they count like a
/// working set and are marked only so the log reads as it was trained.
///
/// Integer-valued on the wire, in the server's order. An unrecognised kind keeps the number it
/// arrived as, so a set resumed from the server is sent back as what it was.
enum SetKind: Hashable, Sendable {
    case working
    case warmUp
    case drop
    case failure
    case unrecognised(Int)

    /// A kind the wire left out is a working set: that is every set logged before kinds existed.
    init(wire: Int?) {
        switch wire {
        case nil, 0?: self = .working
        case 1?: self = .warmUp
        case 2?: self = .drop
        case 3?: self = .failure
        case let other?: self = .unrecognised(other)
        }
    }

    var wire: Int {
        switch self {
        case .working: 0
        case .warmUp: 1
        case .drop: 2
        case .failure: 3
        case .unrecognised(let value): value
        }
    }

    /// Whether the set is the work itself rather than preparation for it. Every kind but the
    /// warm-up is, an unrecognised one included: counting a set the athlete did is the recoverable
    /// mistake, and dropping it from the totals is not.
    var isWork: Bool {
        switch self {
        case .warmUp: false
        case .working, .drop, .failure, .unrecognised: true
        }
    }

    /// Whether the set takes a place in the plan — the nth working set of a movement, which a target,
    /// a routine's count and last time's set at the same position are read against. A warm-up
    /// prepares for the work and a drop set extends the set before it, so neither takes one; a drop
    /// set still counts toward totals and records, which `isWork` answers.
    var takesAPlace: Bool {
        switch self {
        case .working, .failure, .unrecognised: true
        case .warmUp, .drop: false
        }
    }

    /// The kinds the entry row offers.
    static let offered: [SetKind] = [.working, .warmUp, .drop, .failure]

    /// Each set's number among one movement's sets, counted within its kind of work: the working
    /// sets 1, 2, 3 and the warm-ups apart, so a warm-up does not make the first working set
    /// "set 2". A fold, because each number depends on how many of its kind came before.
    static func numbers(of kinds: [SetKind]) -> [Int] {
        kinds.reduce(into: (numbers: [Int](), work: 0, warmUps: 0)) { state, kind in
            if kind.isWork {
                state.work += 1
                state.numbers.append(state.work)
            } else {
                state.warmUps += 1
                state.numbers.append(state.warmUps)
            }
        }.numbers
    }
}
