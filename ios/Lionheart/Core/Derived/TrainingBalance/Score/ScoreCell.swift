import Foundation

/// One measurement of the score as the breakdown draws it: what it is, what it measured against its
/// target, and what it earned, placed on the score's 0–100 axis.
struct ScoreCell: Equatable, Sendable, Identifiable {
    enum Unit: Equatable, Sendable { case hardSets, minutes, sessions }

    /// A count against a target, drawn as a bar with a tick; or a priority lift's standing, which
    /// is a verdict rather than a quantity.
    enum Reading: Equatable, Sendable {
        case quantity(measured: Double, target: Double, unit: Unit)
        case standing(String)
    }

    let id: String
    let label: String
    let reading: Reading
    /// `nil` when the part is unknown — out of reach, not zero — or when the measurement is shown
    /// outside the score, where `outOf` is 0 and no points are at stake.
    let points: Double?
    let outOf: Double
    /// Where the cell begins on the score's axis.
    let start: Double

    var share: Double {
        guard let points, outOf > 0 else { return 0 }
        return min(1, max(0, points / outOf))
    }

    /// "6 of 8 hard sets", "33 of 150 min", "12-week best".
    var figure: String {
        switch reading {
        case .quantity(let measured, let target, let unit):
            let unit = switch unit {
            case .hardSets: "hard sets"
            case .minutes: "min"
            case .sessions: "sessions"
            }
            return "\(MeasurementText.plain(measured.rounded())) of \(MeasurementText.plain(target)) \(unit)"
        case .standing(let words):
            return words
        }
    }
}
