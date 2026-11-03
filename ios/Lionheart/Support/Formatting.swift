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
}
