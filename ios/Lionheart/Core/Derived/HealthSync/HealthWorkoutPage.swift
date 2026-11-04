import Foundation

/// One page of workouts Health added after an anchor, and the anchor that follows them.
struct HealthWorkoutPage: Equatable, Sendable {
    /// Oldest first.
    let workouts: [HealthWorkoutImport]
    /// Where the next page starts: HealthKit's anchor, archived.
    let anchor: Data?
    /// Whether Health had nothing more after this page.
    let isLast: Bool
}
