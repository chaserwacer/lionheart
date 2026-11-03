import Foundation

/// Turning domain values into the strings a screen shows.
///
/// It is in `Support/` and not `Core/` because it is presentation: the decisions behind these
/// strings — which day counts as "yesterday", whether a readiness is worth showing — are already
/// made in `Core/Derived/`, and this only chooses words for them.
enum Formatting {
    /// Elapsed time as a clock. Hours appear only once there are any, so a 40-minute session reads
    /// `40:12` rather than `00:40:12`.
    static func clock(seconds: TimeInterval) -> String {
        let whole = Int(max(0, seconds))
        let (hours, minutes, remainder) = (whole / 3600, (whole % 3600) / 60, whole % 60)
        return hours > 0
            ? String(format: "%d:%02d:%02d", hours, minutes, remainder)
            : String(format: "%d:%02d", minutes, remainder)
    }

    /// A session's duration in words — "48 min", "1 h 12 min".
    static func duration(seconds: TimeInterval) -> String {
        let minutes = Int((max(0, seconds) / 60).rounded())
        guard minutes >= 60 else { return "\(minutes) min" }
        return minutes % 60 == 0 ? "\(minutes / 60) h" : "\(minutes / 60) h \(minutes % 60) min"
    }

    /// How a day reads next to today.
    static func day(_ relative: RelativeDay, calendar: Calendar = .current) -> String {
        switch relative {
        case .today: "Today"
        case .yesterday: "Yesterday"
        case .tomorrow: "Tomorrow"
        case .daysAgo(let days): "\(days) days ago"
        case .inDays(let days): "In \(days) days"
        case .distant(let day): date(day, calendar: calendar)
        }
    }

    static func date(_ day: CalendarDay, calendar: Calendar = .current) -> String {
        day.middayDate(in: calendar).formatted(.dateTime.weekday(.abbreviated).month(.abbreviated).day())
    }

    /// A month as a section header — "October 2026".
    static func month(_ day: CalendarDay, calendar: Calendar = .current) -> String {
        day.middayDate(in: calendar).formatted(.dateTime.month(.wide).year())
    }


    /// What a session log row says under its headline, or `nil` where the state has nothing to
    /// count. The facts come from `Core/`; this only chooses the words and the separator.
    static func sessionFacts(_ facts: LoggedSessionFacts) -> String? {
        switch facts {
        case .performed(let setCount, let duration, let hasEffort):
            var parts = [count(setCount, "set")]
            if let duration { parts.append(self.duration(seconds: duration)) }
            if hasEffort { parts.append("effort rated") }
            return parts.joined(separator: " · ")
        case .inProgress(let setCount):
            return "\(count(setCount, "set")) so far"
        case .prescribed(let prescriptions):
            return prescriptions == 0 ? "No prescriptions" : count(prescriptions, "prescription")
        case .none:
            return nil
        }
    }


    /// A facet's axis in the athlete's words. The axes are a fixed set, so these are a fixed list;
    /// an axis this build has not heard of is named by its wire value rather than hidden, because
    /// the facet's own value is still worth reading.
    static func facetAxis(_ axis: FacetAxis) -> String {
        switch axis {
        case .modality: "Modality"
        case .movementPattern: "Pattern"
        case .muscleRegion: "Region"
        case .intent: "Intent"
        case .lifeDomain: "Life domain"
        case .bodyState: "Body state"
        case .unrecognised(let wire): "Axis \(wire)"
        }
    }

    /// A count with its noun, pluralised. The noun is passed in singular.
    static func count(_ value: Int, _ singular: String, plural: String? = nil) -> String {
        value == 1 ? "1 \(singular)" : "\(value) \(plural ?? singular + "s")"
    }

    // MARK: - Sets

    /// A measurement with no trailing zeros: `100`, `102.5`, `2.25`.
    ///
    /// Plate maths gives halves and quarters and nothing finer, so two decimals is the whole range,
    /// and a weight that happens to be round should read round.
    static func measure(_ value: Double) -> String {
        value.formatted(.number.precision(.fractionLength(0...2)).grouping(.never))
    }

    static func weight(_ value: Double, _ unit: WeightUnit) -> String {
        "\(measure(value)) \(unit.abbreviation)"
    }

    static func distance(_ value: Double, _ unit: DistanceUnit) -> String {
        "\(measure(value)) \(unit.abbreviation)"
    }

    /// What a set did, in one line. `nil` for a set whose words are the record — a freeform set's
    /// text is the row, not a figure beside one.
    static func set(_ command: SetCommand) -> String? {
        switch command {
        case .lift(let set):
            return "\(set.reps) × \(weight(set.weight, set.unit))"
        case .distanceTime(let set):
            return "\(distance(set.distance, set.unit)) in \(clock(seconds: set.duration))"
        case .trailRun(let set):
            return "\(distance(set.distance, set.unit)) in \(clock(seconds: set.duration))"
                + " · \(measure(set.elevationGainMeters)) m gain"
        case .freeform:
            return nil
        }
    }

    /// What a set was asked to do.
    static func target(_ prescription: SetPrescription) -> String {
        switch prescription {
        case .lift(let target):
            let numbers = "\(target.targetReps) × \(weight(target.targetWeight, target.unit))"
            guard let rpe = target.targetRpe.value else { return numbers }
            return "\(numbers) @ \(self.rpe(rpe))"
        case .distanceTime(let target):
            return "\(distance(target.targetDistance, target.unit))"
                + " in \(clock(seconds: target.targetDuration))"
        case .freeform(let text):
            return text
        case .unrecognised:
            // A target this build cannot read still occupies a slot in the plan, so it says so
            // rather than vanishing and making the session look shorter than it is.
            return "A target this version cannot show"
        }
    }

    /// An RPE as the athlete writes it: `8`, `7.5`.
    static func rpe(_ value: Double) -> String {
        "RPE \(measure(value))"
    }

    /// A running volume total — `4,250 kg`, with the grouping a four-figure number wants.
    static func volume(_ total: WorkoutTotals.WeightTotal) -> String {
        "\(total.volume.formatted(.number.precision(.fractionLength(0...1)))) \(total.unit.abbreviation)"
    }
}

extension String {
    /// The string, or `nil` when it holds nothing but whitespace — for the many wire members where
    /// the server writes `""` and the domain means "absent".
    var nonEmpty: String? {
        let trimmed = trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }
}
