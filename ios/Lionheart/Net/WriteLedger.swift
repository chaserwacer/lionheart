import Foundation
import Synchronization

/// How many writes the server has accepted from this device since launch.
///
/// The count, not the writes: a screen only needs to know whether something was written since it
/// last read (`ReadFreshness`), and every write passes through `APIClient`, so counting there
/// catches a set pushed from the workout, a session skipped on Train and a sync started from Today
/// alike without any of them having to know which screens show what they changed.
///
/// A class with a `Mutex` because requests finish on whichever task ran them.
final class WriteLedger: Sendable {
    private let count = Mutex(0)

    var accepted: Int { count.withLock { $0 } }

    func recordAccepted() {
        count.withLock { $0 += 1 }
    }
}
