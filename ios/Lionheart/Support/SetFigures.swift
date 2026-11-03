import Foundation

/// One set as the columns a set row draws: the load, the work done against it, and its rating.
///
/// A set row is read down the column — "where did the load go, where did the reps fall off" — so
/// each figure is its own string rather than one sentence, and the row can align them. The logging
/// surface and a finished session's detail both draw sets this way, from a `SetCommand` and a
/// `SetEntry` respectively, so the two constructors are the one place either is turned into words.
///
/// Load leads, then reps — `145 kg × 5` — which is how records already read (`Formatting.lift`),
/// so a set and the record it set read the same way round.
enum SetFigures: Equatable, Sendable {
    /// A set with numbers.
    case measured(Measured)
    /// A set whose words are the record — freeform, or a shape this build cannot read.
    case described(String)
    /// A freeform set logged with no words at all. A case rather than an empty description,
    /// because the row says so in its own quieter voice instead of drawing a blank.
    case undescribed

    struct Measured: Equatable, Sendable {
        /// The load or the distance, without its unit: `145`, `8.2`.
        let amount: String
        let unit: String
        /// What was done against the amount: `× 5`, or a time, `48:30`.
        let work: String
        /// A figure only some shapes carry — a trail run's climb, `+310 m`.
        let extra: String?
        let rpe: Double?
        /// The set as one phrase, for VoiceOver, which cannot read column position.
        let spoken: String
    }

    static func of(_ command: SetCommand) -> SetFigures {
        switch command {
        case .lift(let set):
            .measured(lift(reps: set.reps, weight: set.weight, unit: set.unit, rpe: set.rpe))
        case .distanceTime(let set):
            .measured(timed(set.distance, set.unit, set.duration, gain: nil, rpe: set.rpe))
        case .trailRun(let set):
            .measured(timed(set.distance, set.unit, set.duration, gain: set.elevationGainMeters, rpe: set.rpe))
        case .freeform(let text):
            text.nonEmpty.map(SetFigures.described) ?? .undescribed
        }
    }

    static func of(_ entry: SetEntry) -> SetFigures {
        switch entry {
        case .lift(let set):
            .measured(lift(reps: set.reps, weight: set.weight, unit: set.unit, rpe: set.rpe))
        case .distanceTime(let set):
            .measured(timed(set.distance, set.unit, set.duration, gain: nil, rpe: set.rpe))
        case .trailRun(let set):
            .measured(timed(set.distance, set.unit, set.duration, gain: set.elevationGainMeters, rpe: set.rpe))
        case .freeform(let common):
            common.source.rawText?.nonEmpty.map(SetFigures.described) ?? .undescribed
        case .unrecognised(let type, let common):
            // The shape is unknown to this build, but the words it was logged with — if it was
            // texted — are still the athlete's own record of it.
            .described(common.source.rawText?.nonEmpty ?? "A set shape this version cannot read (\(type))")
        }
    }

    private static func lift(reps: Int, weight: Double, unit: WeightUnit, rpe: Rpe) -> Measured {
        Measured(
            amount: Formatting.measure(weight),
            unit: unit.abbreviation,
            work: "× \(reps)",
            extra: nil,
            rpe: rpe.value,
            spoken: "\(Formatting.weight(weight, unit)) for \(Formatting.count(reps, "rep"))")
    }

    private static func timed(
        _ distance: Double, _ unit: DistanceUnit, _ duration: TimeInterval, gain: Double?, rpe: Rpe
    ) -> Measured {
        let climb = gain.map { "\($0 < 0 ? "−" : "+")\(Formatting.measure(abs($0))) m" }
        let spokenClimb = gain.map { ", \(Formatting.measure($0)) m gain" } ?? ""
        return Measured(
            amount: Formatting.measure(distance),
            unit: unit.abbreviation,
            work: Formatting.clock(seconds: duration),
            extra: climb,
            rpe: rpe.value,
            spoken: "\(Formatting.distance(distance, unit)) in \(Formatting.clock(seconds: duration))\(spokenClimb)")
    }
}
