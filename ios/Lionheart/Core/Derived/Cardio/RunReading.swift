import Foundation

/// One logged run as a fitness reading: its pace, its average heart rate, and the heartbeats each
/// kilometre cost.
///
/// Heartbeats per kilometre is average heart rate times minutes per kilometre — what a kilometre
/// cost the heart. It is roughly level across easy and steady paces, since a faster pace raises
/// the rate about as much as it shortens the kilometre, so runs of different paces sit on one
/// trend; as the aerobic base grows, it falls. Pace is elapsed, the only time the cardio report
/// carries for every run; a run's own page has its moving pace.
struct RunReading: Equatable, Sendable, Identifiable {
    let id: String
    let start: Date
    let kilometres: Double
    let secondsPerKilometre: Double
    let averageBpm: Int

    /// The heartbeats one kilometre, or one mile, cost: average rate times minutes per unit.
    func beats(per unit: RunUnit) -> Double {
        Double(averageBpm) * secondsPerKilometre * (unit.meters / 1000) / 60
    }

    /// The share of a run's clock time that must have heart rate behind it for its average to stand
    /// for the run — the server's own floor for reading a window's zones.
    static let coverageFloor = 0.5

    /// The shortest run read: under a kilometre, the warm-up is most of it.
    static let shortestMetres = 1000.0

    /// The span's logged runs with distance and heart rate enough to read, oldest first.
    static func of(_ report: CardioReport) -> [RunReading] {
        report.sessions.compactMap { session in
            guard case .loggedSession = session.source,
                  let metres = session.distanceMeters, metres >= shortestMetres,
                  case .recorded(let average, _, let coverage, _, _) = session.heartRate,
                  coverage >= coverageFloor, session.minutes > 0
            else { return nil }
            let kilometres = metres / 1000
            return RunReading(
                id: session.id, start: session.start, kilometres: kilometres,
                secondsPerKilometre: session.minutes * 60 / kilometres, averageBpm: average)
        }
    }
}
