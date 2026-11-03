import Foundation

/// The athlete's own rating of his days beside Oura's readiness for the same days — whether his
/// sense of how recovered he is agrees with the ring's.
///
/// Two plain readings rather than one clever one: the mean readiness on the days he rated low,
/// middling and high, which needs no statistics to read, and the correlation between the two, which
/// says how closely they move together across every day that has both. Neither is a verdict on
/// which is right; a ring and a mind can disagree for good reasons.
struct WellnessAgainstReadiness: Equatable, Sendable {
    /// One rated day, with Oura's readiness for it where the ring filed one.
    struct Day: Equatable, Sendable, Identifiable {
        let state: WellnessState
        let readiness: OuraScore?

        var id: CalendarDay { state.date }
    }

    /// The mean readiness on the days whose overall rating fell in one band.
    struct Band: Equatable, Sendable, Identifiable {
        let label: String
        let days: Int
        let meanReadiness: Double

        var id: String { label }
    }

    /// Every rated day in the range, oldest first.
    let days: [Day]
    /// Oura's readiness on every day in the range that has one, oldest first — the chart's line.
    let readiness: [MetricPoint]

    /// The fewest days carrying both before a correlation is worth stating.
    static let minimumPairs = 8

    /// The days with both a rating and a readiness.
    var paired: [Day] { days.filter { $0.readiness != nil } }

    /// Mean readiness for the days rated low (under 2.5), middling, and high (3.5 and up), each band
    /// that holds a day.
    var bands: [Band] {
        let bounds: [(String, (Double) -> Bool)] = [
            ("Rated under 2.5", { $0 < 2.5 }),
            ("Rated 2.5 to 3.4", { $0 >= 2.5 && $0 < 3.5 }),
            ("Rated 3.5 and up", { $0 >= 3.5 }),
        ]
        return bounds.compactMap { label, contains in
            let inBand = paired.filter { contains($0.state.overallScore) }.compactMap(\.readiness?.value)
            guard !inBand.isEmpty else { return nil }
            return Band(label: label, days: inBand.count, meanReadiness: inBand.reduce(0, +) / Double(inBand.count))
        }
    }

    /// Pearson's r between his overall rating and Oura's readiness, or `nil` with fewer than
    /// `minimumPairs` days carrying both, or when either never varies.
    var correlation: Double? {
        let pairs = paired.compactMap { day in day.readiness.map { (day.state.overallScore, $0.value) } }
        guard pairs.count >= Self.minimumPairs else { return nil }
        return Self.pearson(pairs)
    }

    static func of(entries: [WellnessState], ouraDays: [OuraDay]) -> WellnessAgainstReadiness {
        let readinessByDay = Dictionary(
            ouraDays.compactMap { day in day.readiness?.score.map { (day.date, $0) } },
            uniquingKeysWith: { first, _ in first })
        return WellnessAgainstReadiness(
            days: entries
                .sorted { $0.date < $1.date }
                .map { Day(state: $0, readiness: readinessByDay[$0.date]) },
            readiness: readinessByDay
                .map { MetricPoint(day: $0.key, value: $0.value.value) }
                .sorted { $0.day < $1.day })
    }

    /// How closely two series move together, from −1 to 1.
    static func pearson(_ pairs: [(Double, Double)]) -> Double? {
        let count = Double(pairs.count)
        guard count > 1 else { return nil }
        let meanX = pairs.reduce(0) { $0 + $1.0 } / count
        let meanY = pairs.reduce(0) { $0 + $1.1 } / count
        let covariance = pairs.reduce(0) { $0 + ($1.0 - meanX) * ($1.1 - meanY) }
        let spreadX = pairs.reduce(0) { $0 + ($1.0 - meanX) * ($1.0 - meanX) }.squareRoot()
        let spreadY = pairs.reduce(0) { $0 + ($1.1 - meanY) * ($1.1 - meanY) }.squareRoot()
        guard spreadX > 0, spreadY > 0 else { return nil }
        return covariance / (spreadX * spreadY)
    }

    /// The correlation in plain words: how strongly the two move together.
    static func strength(of r: Double) -> String {
        switch abs(r) {
        case ..<0.1: "no relation"
        case ..<0.3: "a weak relation"
        case ..<0.5: "a moderate relation"
        default: "a strong relation"
        }
    }
}
