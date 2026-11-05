import SwiftUI

/// The going-in state's words and colours, in one place so the header, the compact line and the
/// sheet say the same thing.
enum GoingInWords {
    static func name(_ kind: GoingInReading.Part.Kind) -> String {
        switch kind {
        case .readiness: "Readiness"
        case .autonomic: "HRV and heart rate"
        case .sleep: "Sleep"
        case .load: "Load"
        case .wellness: "Wellness"
        }
    }

    /// The name inside a sentence, where only HRV keeps its capitals.
    static func inlineName(_ kind: GoingInReading.Part.Kind) -> String {
        kind == .autonomic ? name(kind) : name(kind).lowercased()
    }

    /// For a column a fifth of the row wide.
    static func shortName(_ kind: GoingInReading.Part.Kind) -> String {
        switch kind {
        case .readiness: "Readiness"
        case .autonomic: "HRV/HR"
        case .sleep: "Sleep"
        case .load: "Load"
        case .wellness: "Wellness"
        }
    }

    static func score(_ rating: GoingInRating) -> String {
        rating.score.map(String.init) ?? "–"
    }

    static func band(_ rating: GoingInRating) -> String {
        switch rating {
        case .rated(_, .high): "High"
        case .rated(_, .normal): "Normal"
        case .rated(_, .reduced): "Reduced"
        case .rated(_, .low): "Low"
        case .rated(_, .unrecognised): "Rated"
        case .notRated: "Not rated"
        case .unrecognised: "Not readable"
        }
    }

    static func tint(_ rating: GoingInRating) -> Color {
        tint(score: rating.score)
    }

    /// Colour carries the band, never decoration: green over his usual day, blue on it — not the
    /// accent, Brick, which would read as a warning — orange and red under.
    static func tint(score: Int?) -> Color {
        switch score {
        case .none: Color(.tertiaryLabel)
        case .some(80...): .green
        case .some(65...): .blue
        case .some(50...): .orange
        case .some: .red
        }
    }

    /// The line under the number: the part holding it down, and what it was made without.
    static func verdict(_ reading: GoingInReading) -> String {
        if case .notRated = reading.rating {
            return "No readiness, night or sleep score to rate from yet."
        }
        var lines: [String] = []
        if let weakest = reading.weakest, let score = weakest.score.score {
            lines.append("\(name(weakest.kind)) \(score) holds it down.")
        } else {
            lines.append("Every part read is at your usual day or above.")
        }
        // Wellness is often not rated and says the least, so its absence is not worth a line.
        let unread = reading.unread.filter { $0 != .wellness }
        if !unread.isEmpty {
            lines.append("Not read yet: \(unread.map(inlineName).joined(separator: ", ")).")
        }
        return lines.joined(separator: " ")
    }

    static func muscles(_ muscles: [MuscleGroup]) -> String {
        let shown = muscles.prefix(4).map(\.name)
        let rest = muscles.count - shown.count
        return shown.joined(separator: ", ") + (rest > 0 ? " +\(rest)" : "")
    }

    /// "Lift first: push and pull", then the cardio after it.
    static func upNext(_ upNext: GoingInReading.UpNextLine) -> String {
        let lift = upNext.families.isEmpty ? "Lift" : PatternFamily.listed(upNext.families).capitalisingFirst
        let cardio = cardio(upNext.cardio)
        switch upNext.first {
        case .lift: return cardio.map { "\(lift) first, then \($0)" } ?? lift
        case .cardio: return cardio.map { "\($0.capitalisingFirst) first, then \(lift.lowercased())" } ?? lift
        case .unrecognised: return lift
        }
    }

    static func cardio(_ cardio: NextCardio) -> String? {
        switch cardio {
        case .zone2(let minutes, _): "\(Formatting.minutes(Double(minutes))) zone 2"
        case .quality(let bouts, let boutMinutes, _, _): "\(bouts) × \(Formatting.minutes(Double(boutMinutes))) quality"
        case .unrecognised: nil
        }
    }

    static func aim(_ aim: LiftAim) -> String {
        switch aim {
        // On system load the figure is bodyweight and plate together, so it is said as such.
        case .beat(let load, let reps, let unit, _, _, let onSystemLoad):
            "Beat \(Formatting.weight(load, unit))\(onSystemLoad ? " system" : "") × \(reps)"
        case .hold(let load, let reps, let unit, _, let onSystemLoad):
            "Hold \(Formatting.weight(load, unit))\(onSystemLoad ? " system" : "") × \(reps)"
        case .establish: "Set a mark"
        case .unrecognised: ""
        }
    }

    static func spoken(_ part: GoingInReading.Part) -> String {
        part.score.score.map { "\(name(part.kind)), \($0)" } ?? "\(name(part.kind)), not read"
    }
}
