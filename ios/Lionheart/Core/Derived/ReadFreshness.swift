import Foundation

/// When a screen last had the server's current answer, in time and in writes.
///
/// Writes are counted because they are what changes the athlete's data: a set pushed, a session
/// finished, a sync stored. Counting the ones the server accepted on this device tells a screen
/// that its answer may be out of date without asking the server whether it is.
struct ReadStamp: Equatable, Sendable {
    let readAt: Date
    /// `WriteLedger.accepted` when the read was sent.
    let acceptedWrites: Int
}

/// Whether a screen coming back into view should ask the server again.
///
/// A tab's `.task` runs on every return to it. Reading on every return costs a request burst —
/// the whole training log, on History — for an answer that almost never changed in the seconds
/// between two taps on the tab bar. Reading on none leaves a screen behind a write made on another
/// tab. The rule between them:
///
/// - a screen with nothing current on it reads: never loaded, failed, or showing what the device
///   or a failed refresh left behind;
/// - a screen whose answer came before a write this device made reads, since that write may be on
///   it;
/// - a screen read on an earlier calendar day reads, since "today" has moved under it;
/// - otherwise a screen reads once its answer is older than the span it stays fresh for, which
///   covers what changes without a write from here — the web app, a sync run elsewhere.
enum ReadFreshness {
    static func needsRead<Value>(
        _ state: LoadState<Value>,
        lastRead: ReadStamp?,
        now: Date,
        acceptedWrites: Int,
        freshFor: TimeInterval,
        calendar: Calendar = .current
    ) -> Bool {
        switch state {
        case .loading, .unavailable, .stale: return true
        case .loaded: break
        }
        guard let lastRead else { return true }
        let age = now.timeIntervalSince(lastRead.readAt)
        return lastRead.acceptedWrites != acceptedWrites
            || age < 0
            || age >= freshFor
            || CalendarDay(lastRead.readAt, in: calendar) != CalendarDay(now, in: calendar)
    }
}
