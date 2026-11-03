import Foundation

/// One sample of Oura's all-day heart-rate series.
///
/// Not evenly spaced — nominally five minutes, faster during a workout, absent while the ring is
/// off — so each carries its own instant, and a chart draws the gaps as gaps.
struct HeartRateSample: Equatable, Sendable {
    let at: Date
    let bpm: Int
    /// What the ring was doing — `awake`, `rest`, `sleep`, `workout`, `session`, `live` — as Oura
    /// sent it.
    let source: String
}

extension HeartRateSample: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let at = container.instant("at"), let bpm = container.lenient(Int.self, "bpm") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A heart-rate sample needs its instant and rate."))
        }
        self.init(at: at, bpm: bpm, source: container.lenient("source", or: ""))
    }
}
