import Foundation

/// The ring workouts a span left out, as one sentence: one count per reason, largest first.
///
/// The server counts each category and reason apart, so a walk and a handball game both left out
/// for low heart rate arrive as two rows; read row by row they would name the same reason twice.
enum LeftOut {
    static func sentence(_ excluded: [ExcludedWorkouts]) -> String? {
        let counts = excluded.reduce(into: [(reason: ExcludedWorkouts.Reason, count: Int)]()) { merged, row in
            if let index = merged.firstIndex(where: { $0.reason == row.reason }) {
                merged[index].count += row.count
            } else {
                merged.append((row.reason, row.count))
            }
        }
        guard !counts.isEmpty else { return nil }
        return counts
            .sorted { $0.count > $1.count }
            .map { "\($0.count) \(phrase($0.reason, count: $0.count))" }
            .joined(separator: ", ")
    }

    /// The reason after its count, singular for one.
    static func phrase(_ reason: ExcludedWorkouts.Reason, count: Int) -> String {
        guard count == 1 else { return reason.phrase }
        return switch reason {
        case .shortWalk: "walk under 30 minutes"
        case .heartRateUnread: "workout with too little heart rate to show training"
        case .heartRateBelowTraining: "workout whose heart rate stayed below training"
        case .detectedGame: "game or dance the ring guessed"
        case .sameAsLoggedRun, .insideLoggedLifting, .strength, .notTraining, .noDuration, .unrecognised: reason.phrase
        }
    }
}
