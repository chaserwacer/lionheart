import Foundation

/// A metric's samples thinned for drawing.
enum MetricTrace {
    /// More points than a phone-width chart has pixels to put them on.
    static let drawnPoints = 400

    /// The samples as a line of at most `limit` points: each the mean of an equal run of samples,
    /// at the run's first instant, so a trainer's per-second power reads as its shape rather than
    /// as a solid band. A series already that short is drawn as it is.
    static func drawn(_ samples: [MetricSample], limit: Int = drawnPoints) -> [MetricSample] {
        guard limit > 0, samples.count > limit else { return samples }
        let size = Int((Double(samples.count) / Double(limit)).rounded(.up))
        return stride(from: 0, to: samples.count, by: size).map { first in
            let run = samples[first..<min(first + size, samples.count)]
            return MetricSample(at: run[run.startIndex].at, value: run.map(\.value).reduce(0, +) / Double(run.count))
        }
    }
}
