import Foundation

/// A self-reported daily reading the athlete enters.
///
/// The four component scores are on a 1–5 scale and `overallScore` is the server's own average of
/// them — with stress inverted, since a low stress score is a good day. It is received rather than
/// recomputed so the two clients and the analyses cannot disagree about one number.
///
/// Identified by its day, for the same reason as `OuraDay`: one reading per athlete per day, and
/// no route takes the server's `StateID`.
struct WellnessState: Equatable, Sendable, Identifiable {
    let date: CalendarDay
    let motivationScore: Int
    let stressScore: Int
    let moodScore: Int
    let energyScore: Int
    let overallScore: Double

    var id: CalendarDay { date }
}

extension WellnessState: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        guard let date = container.day("date") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "A wellness state without a date cannot be placed on a timeline."))
        }

        self.init(
            date: date,
            motivationScore: container.lenient("motivationScore", or: 0),
            stressScore: container.lenient("stressScore", or: 0),
            moodScore: container.lenient("moodScore", or: 0),
            energyScore: container.lenient("energyScore", or: 0),
            overallScore: container.lenient("overallScore", or: 0))
    }
}
