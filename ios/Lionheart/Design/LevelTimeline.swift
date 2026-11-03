import Charts
import SwiftUI

/// A stretch of clock time split into lanes — a hypnogram, a day's activity — each stretch drawn as
/// a bar in its lane.
///
/// One component for every "what state was it in, when" chart, so a night and a day read the same
/// way: lanes top to bottom in the order given, time left to right, an optional shaded band behind
/// the bars for spans worth pointing at (the long sittings of a day). Built on Swift Charts because
/// the stock chart is the right renderer; no stock view lays out categorical lanes over time.
struct LevelTimeline<Lane: Hashable>: View {
    /// One bar: a lane, the time it covers, and how it is drawn.
    struct Span: Identifiable {
        let lane: Lane
        let start: Date
        let end: Date
        let style: Color

        var id: String { "\(start.timeIntervalSince1970)-\(end.timeIntervalSince1970)-\(lane)" }
    }

    /// Top to bottom.
    let lanes: [Lane]
    let laneName: (Lane) -> String
    let spans: [Span]
    let domain: ClosedRange<Date>
    /// Shaded behind every lane — the stretches the screen wants the eye on.
    var highlights: [ClosedRange<Date>] = []
    /// A strip for a list row: no axes, no lane names, the lanes squeezed to the row.
    var isCompact = false

    var body: some View {
        Chart {
            ForEach(Array(highlights.enumerated()), id: \.offset) { _, range in
                RectangleMark(xStart: .value("From", range.lowerBound), xEnd: .value("To", range.upperBound))
                    .foregroundStyle(Color(.systemOrange).opacity(0.12))
            }
            ForEach(spans) { span in
                RectangleMark(
                    xStart: .value("Start", span.start),
                    xEnd: .value("End", span.end),
                    y: .value("Lane", laneName(span.lane)),
                    height: .ratio(isCompact ? 1 : 0.8))
                    .foregroundStyle(span.style)
            }
        }
        .chartXScale(domain: domain)
        .chartYScale(domain: lanes.map(laneName))
        .chartLegend(.hidden)
        .chartYAxis {
            if isCompact {
                AxisMarks { _ in }
            } else {
                AxisMarks(position: .leading) { value in
                    AxisValueLabel {
                        if let name = value.as(String.self) {
                            Text(name).font(.caption2).foregroundStyle(Color(.secondaryLabel))
                        }
                    }
                }
            }
        }
        .chartXAxis {
            if isCompact {
                AxisMarks { _ in }
            } else {
                AxisMarks(values: .stride(by: .hour, count: hourStride)) { value in
                    AxisGridLine().foregroundStyle(Color(.separator))
                    AxisValueLabel {
                        if let date = value.as(Date.self) {
                            // A 24-hour label: across a whole day, "4" and "4" a half-day apart
                            // would be the same mark.
                            Text(String(format: "%02d", Calendar.current.component(.hour, from: date)))
                        }
                    }
                }
            }
        }
    }

    /// Every hour on a night, every three across a day, so the labels never crowd.
    private var hourStride: Int {
        domain.upperBound.timeIntervalSince(domain.lowerBound) > 14 * 3600 ? 3 : 1
    }
}
