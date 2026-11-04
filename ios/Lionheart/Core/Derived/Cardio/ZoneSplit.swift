import Foundation

/// A span's cardio split into easy and hard time, per week of days in the span.
///
/// Zone 2 against zones 3 to 5 is how the athlete divides his cardio: the aerobic base, and the
/// shorter work above it. Quality minutes are the part of the hard time the score credits: work
/// bouts in endurance sessions. Per week of days actually in the span, so a part-week at either end
/// does not read as a light one.
struct ZoneSplit: Equatable, Sendable {
    let zone2PerWeek: Double
    let zones3To5PerWeek: Double
    let qualityPerWeek: Double
    let sessionsPerWeek: Double

    /// Zones 3–5 as a share of the time at zone 2 and above; `nil` with none of either.
    var hardShare: Double? {
        let aerobic = zone2PerWeek + zones3To5PerWeek
        return aerobic > 0 ? zones3To5PerWeek / aerobic : nil
    }

    static func of(_ weeks: [CardioWeek], sessions: [CardioSession] = []) -> ZoneSplit {
        let span = max(1, Double(weeks.reduce(0) { $0 + $1.daysInRange }) / 7)
        return ZoneSplit(
            zone2PerWeek: weeks.reduce(0) { $0 + $1.zones.zone2 } / span,
            zones3To5PerWeek: weeks.reduce(0) { $0 + $1.zones.zone3 + $1.zones.zone4 + $1.zones.zone5 } / span,
            qualityPerWeek: sessions.reduce(0) { $0 + $1.qualityMinutes } / span,
            sessionsPerWeek: Double(weeks.reduce(0) { $0 + $1.sessions }) / span)
    }
}
