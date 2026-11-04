import Foundation

/// The cardio screens' words for the server's verdicts, in one place so the score, the priorities
/// and the cardio screen say the same thing.
enum CardioWords {
    static func unknown(_ reason: CardioUnknownReason) -> String {
        switch reason {
        case .noHeartRate: "Unknown: no heart rate from the ring this week."
        case .noMaximumHeartRate: "Unknown: the profile has no age to draw heart-rate zones from."
        case .sessionsUnrecorded: "Unknown: most of the week's cardio session time has no heart rate behind it."
        case .unrecognised: "Unknown."
        }
    }

    /// Why a ring workout was left out, as the end of its row.
    static func verdict(_ reason: ExcludedWorkouts.Reason) -> String {
        switch reason {
        case .heartRateUnread: "too little heart rate to tell"
        case .heartRateBelowTraining: "below training"
        case .sameAsLoggedRun, .insideLoggedLifting, .shortWalk, .strength, .notTraining, .noDuration, .unrecognised:
            reason.phrase
        }
    }
}
