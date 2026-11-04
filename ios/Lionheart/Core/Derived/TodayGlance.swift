import Foundation

/// What the Home and Lock Screen widgets and the readiness intent show: the morning's verdict and
/// the training either side of today, already in words.
///
/// The app writes it whenever Today has the server's current answer for today, and the widget
/// extension reads it; nothing in the extension reaches the server. So everything here is final —
/// strings the app has already formatted, a score for the gauge — and the extension decides only
/// how a day is named against the moment it draws ("Today", "Yesterday"), which the app cannot
/// know in advance.
///
/// Compiled into both the app and `LionheartWidgets`, which is why it is Foundation and nothing
/// else, and why every member is a plain value: the two processes have to decode one another's
/// bytes, across builds.
struct TodayGlance: Codable, Equatable, Sendable {
    /// Oura's readiness for the newest day the ring has filed one.
    struct Readiness: Codable, Equatable, Sendable {
        let score: Int
        /// Oura's band, in Oura's words.
        let band: String
        /// Whether the band asks for attention — the one band drawn in a signal colour.
        let asksForAttention: Bool
        /// Against his own baseline, signed: "−10 vs baseline". `nil` with too few days to compare.
        let comparison: String?
        /// The score over the baseline window, oldest first, for the line beside it.
        let recent: [Double]
        /// Midday of the day the score belongs to.
        let day: Date
    }

    /// One session, as a line.
    struct Session: Codable, Equatable, Sendable {
        /// Midday of the day it was done, or is planned for.
        let day: Date
        /// Its movements, or its kind where it names none.
        let headline: String
        /// What it came to — "18 sets · 52 min" — or what it prescribes.
        let facts: String?
    }

    let readiness: Readiness?
    let lastSession: Session?
    let nextSession: Session?
    /// When the app had the server's answer this was made from.
    let asOf: Date

    /// Whether a glance's day is the day it is drawn on — the one day a widget leaves unnamed.
    static func isToday(_ day: Date, at now: Date, calendar: Calendar = .current) -> Bool {
        calendar.isDate(day, inSameDayAs: now)
    }

    /// A glance's day named against the moment it is drawn: "Today", "Yesterday", "Tomorrow", the
    /// weekday within the week either side, and the date beyond.
    ///
    /// Decided by the widget at draw time rather than by the app when it wrote the glance, because
    /// the same glance is drawn across midnight: last night's readiness has to turn from "Today"
    /// into "Yesterday" without the app running.
    static func dayName(_ day: Date, at now: Date, calendar: Calendar = .current) -> String {
        let days = calendar.dateComponents(
            [.day], from: calendar.startOfDay(for: now), to: calendar.startOfDay(for: day)).day ?? 0
        switch days {
        case 0: return "Today"
        case -1: return "Yesterday"
        case 1: return "Tomorrow"
        case -6 ... -2, 2...6: return day.formatted(.dateTime.weekday(.wide))
        default: return day.formatted(.dateTime.month(.abbreviated).day())
        }
    }
}
