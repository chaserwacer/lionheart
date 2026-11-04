import Foundation

/// The words a pin's reading is said in, written once for the summary line and the full rows.
enum FocusWords {
    static func horizon(_ horizon: FocusHorizon) -> String {
        let style = Date.FormatStyle.dateTime.month(.abbreviated).day()
        return "\(horizon.starts.middayDate().formatted(style)) – \(horizon.ends.middayDate().formatted(style.year()))"
    }

    static func phase(_ phase: FocusReading.Phase) -> String {
        switch phase {
        case .upcoming(let days): days == 1 ? "starts tomorrow" : "starts in \(days) days"
        case .running: phase.week.map { "week \($0.current) of \($0.of)" } ?? ""
        case .horizonOver: "horizon over"
        case .unpinned(let on): "unpinned \(on.middayDate().formatted(.dateTime.month(.abbreviated).day()))"
        case .unrecognised: ""
        }
    }

    static func attendance(_ attendance: FocusReading.Attendance) -> String {
        let expected = Int(attendance.expectedSoFar.rounded())
        return "\(attendance.sessions) of \(expected) so far · \(attendance.expectedOverHorizon) in all"
    }

    static func progress(_ progress: FocusReading.Progress) -> String {
        let unit = progress.unit
        let pounds = { (value: Double) in Formatting.measure(Imperial.pounds(value, from: unit).rounded()) }
        let before = progress.before.map { pounds($0.value) } ?? "—"
        let best = progress.best.map { pounds($0.value) } ?? "—"
        let change = progress.change.map { " (\($0 < 0 ? "−" : "+")\(pounds(abs($0))))" } ?? ""
        return "\(before) → \(best) \(Imperial.weightLabel(unit))\(change)"
    }

    static func target(_ target: FocusReading.TargetStanding) -> String? {
        switch target {
        case .untargeted, .unreadable:
            return nil
        case .met(let on, let weight, let reps):
            return "Met \(on.middayDate().formatted(.dateTime.month(.abbreviated).day())), \(Formatting.measure(weight)) × \(reps)"
        case .toGo(let weight, let unit, let reps, let best):
            let goal = "\(Formatting.weight(weight, unit)) × \(reps)"
            guard let best else { return "\(goal) · no set of \(reps) yet" }
            return "\(goal) · \(Formatting.weight(max(0, weight - best), unit)) to go"
        }
    }

    /// The footnote line: sessions against the frequency, the last session, the target.
    static func summary(_ reading: FocusReading) -> String {
        let attendance = reading.attendance
        var parts = ["\(attendance.sessions) of \(Int(attendance.expectedSoFar.rounded())) sessions"]
        if let last = reading.lastDone {
            parts.append("last \(inSentence(RelativeDay.of(last, today: CalendarDay(Date()))))")
        }
        switch reading.target {
        case .met: parts.append("target met")
        case .toGo(_, let unit, let reps, let best):
            parts.append(reading.target.remaining.map { "\(Formatting.weight($0, unit)) to target" }
                ?? (best == nil ? "no set of \(reps) yet" : ""))
        case .untargeted, .unreadable: break
        }
        return parts.filter { !$0.isEmpty }.joined(separator: " · ")
    }

    /// A day as it reads mid-sentence: "yesterday", "3 days ago", or the date.
    private static func inSentence(_ day: RelativeDay) -> String {
        switch day {
        case .today: "today"
        case .yesterday: "yesterday"
        case .daysAgo(let days): "\(days) days ago"
        case .tomorrow, .inDays, .distant: Formatting.day(day)
        }
    }
}
