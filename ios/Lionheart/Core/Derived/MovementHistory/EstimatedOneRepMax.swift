import Foundation

/// The single rep a set implies, by Epley — `weight × (1 + reps / 30)` — as the athlete's own
/// analysis computes it: **no estimate at all above twelve reps**.
///
/// The server's records carry Epley too (`RecordMeasures.EstimatedOneRepMax`), uncapped, because a
/// record never sits above the twelve-rep max. A chart over every session is different: a set of
/// twenty-five estimates a single nobody could lift and draws a spike through the line, so a
/// set past the cap estimates nothing rather than something false. A set with no load estimates
/// nothing either — a bodyweight dip recorded at zero added weight has no single to imply.
///
/// Derived here rather than read from the server because no route carries a per-set estimate:
/// the history is the whole log, already on the device, and the formula is the one the server's
/// records use.
enum EstimatedOneRepMax {
    /// Sets of more reps than this estimate nothing.
    static let maximumReps = 12

    static func of(weight: Double, reps: Int) -> Double? {
        guard weight > 0, (1...maximumReps).contains(reps) else { return nil }
        return reps == 1 ? weight : weight * (1 + Double(reps) / 30)
    }
}
