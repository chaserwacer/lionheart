import Foundation

/// A wellness state being entered: the day, and the athlete's four ratings of it, each 1–5.
///
/// A rating starts unchosen rather than at a middle value: a 3 nobody chose would be logged as the
/// athlete's own sense of the day. So the draft is loggable only once all four are chosen.
///
/// One state per day is the server's rule, and logging a day again replaces it — which is why the
/// draft opens on that day's entry where there is one, and says it will replace it.
struct WellnessDraft: Equatable, Sendable {
    var day: CalendarDay
    var energy: Int?
    var mood: Int?
    var motivation: Int?
    var stress: Int?

    /// The scale every rating is on.
    static let scale = 1...5

    /// The four ratings, in the order the form asks them.
    enum Rating: CaseIterable, Hashable, Sendable, Identifiable {
        case energy, mood, motivation, stress

        var id: Self { self }

        var name: String {
            switch self {
            case .energy: "Energy"
            case .mood: "Mood"
            case .motivation: "Motivation"
            case .stress: "Stress"
            }
        }

        /// What the two ends of the scale mean, said under the control.
        var ends: (low: String, high: String) {
            switch self {
            case .energy: ("1 drained", "5 full of it")
            case .mood: ("1 low", "5 high")
            case .motivation: ("1 none", "5 driven")
            case .stress: ("1 calm", "5 very stressed")
            }
        }
    }

    /// A draft for `day`, opened on the entry already there for it, if any.
    static func opening(on day: CalendarDay, existing: [WellnessState]) -> WellnessDraft {
        guard let state = existing.first(where: { $0.date == day }) else {
            return WellnessDraft(day: day, energy: nil, mood: nil, motivation: nil, stress: nil)
        }
        return WellnessDraft(
            day: day,
            energy: state.energyScore,
            mood: state.moodScore,
            motivation: state.motivationScore,
            stress: state.stressScore)
    }

    subscript(rating: Rating) -> Int? {
        get {
            switch rating {
            case .energy: energy
            case .mood: mood
            case .motivation: motivation
            case .stress: stress
            }
        }
        set {
            let value = newValue.flatMap { Self.scale.contains($0) ? $0 : nil }
            switch rating {
            case .energy: energy = value
            case .mood: mood = value
            case .motivation: motivation = value
            case .stress: stress = value
            }
        }
    }

    /// The entry these fields amount to, or `nil` until all four are chosen and the day is not in
    /// the future.
    func entry(today: CalendarDay) -> WellnessEntry? {
        guard day <= today, let energy, let mood, let motivation, let stress else { return nil }
        return WellnessEntry(day: day, energy: energy, mood: mood, motivation: motivation, stress: stress)
    }
}

/// A complete wellness rating of one day, as `POST /api/wellness/add` takes it.
struct WellnessEntry: Equatable, Sendable {
    let day: CalendarDay
    let energy: Int
    let mood: Int
    let motivation: Int
    let stress: Int
}
