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

    /// A count with its noun, pluralised. The noun is passed in singular.
    static func count(_ value: Int, _ singular: String, plural: String? = nil) -> String {
        value == 1 ? "1 \(singular)" : "\(value) \(plural ?? singular + "s")"
    }
}
