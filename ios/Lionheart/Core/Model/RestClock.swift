import Foundation

/// The countdown between sets.
///
/// Two cases, and the running one stores *when it started and how long it is* rather than the
/// instant it ends. Both facts are needed on screen — the remaining seconds and the fraction
/// elapsed, which is what makes a bar or a ring possible — and deriving the start back from an end
/// would mean storing the duration anyway.
///
/// Storing instants rather than a tick count is also what makes it survive: the clock is persisted
/// with the workout, so a force-quit halfway through a two-minute rest resumes with the right time
/// left rather than starting over or stopping.
enum RestClock: Equatable, Sendable, Codable {
    case idle
    case running(since: Date, duration: TimeInterval)

    /// How long is left at `now`, never negative. Zero once the rest is over, so a clock the store
    /// has not yet retired still reads as finished rather than counting into the negative.
    func remaining(at now: Date) -> TimeInterval {
        switch self {
        case .idle: 0
        case .running(let since, let duration): max(0, duration - now.timeIntervalSince(since))
        }
    }

    /// How far through the rest is at `now`, in [0, 1]. A rest of no length reads as finished.
    func fractionElapsed(at now: Date) -> Double {
        switch self {
        case .idle:
            return 0
        case .running(_, let duration):
            guard duration > 0 else { return 1 }
            return min(1, max(0, 1 - remaining(at: now) / duration))
        }
    }

    /// Whether this clock still has time to run at `now`.
    ///
    /// The store asks this before sleeping, and the bar asks it to decide whether to show a rest at
    /// all. An idle clock is not resting, which is not the same as a running one that has expired.
    func isResting(at now: Date) -> Bool {
        switch self {
        case .idle: false
        case .running: remaining(at: now) > 0
        }
    }

    var duration: TimeInterval? {
        switch self {
        case .idle: nil
        case .running(_, let duration): duration
        }
    }

    /// The same rest with `extra` added to its length, which is what a "+30 s" control does.
    ///
    /// It lengthens the rest rather than restarting it: the athlete pressing it has been resting
    /// for ninety seconds and wants thirty more, not two minutes more. An idle clock has nothing to
    /// extend and stays idle.
    func extended(by extra: TimeInterval) -> RestClock {
        switch self {
        case .idle: .idle
        case .running(let since, let duration): .running(since: since, duration: max(0, duration + extra))
        }
    }

    /// The same rest as long as `duration` in all, from when it started: a rest made three minutes
    /// forty seconds in has two minutes twenty left, and one made shorter than it has run is over.
    func resized(to duration: TimeInterval) -> RestClock {
        switch self {
        case .idle: .idle
        case .running(let since, _): .running(since: since, duration: max(0, duration))
        }
    }
}
