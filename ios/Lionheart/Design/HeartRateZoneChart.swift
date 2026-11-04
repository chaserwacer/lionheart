import Charts
import SwiftUI

/// Heart rate over clock time against the five zone bands, with dashed rules where a session begins
/// and ends — what a cardio session and a recorded run both draw their heart rate as.
struct HeartRateZoneChart: View {
    let samples: [HeartRateSample]
    let maxHeartRate: MaxHeartRate?
    /// Where the session begins and ends, ruled on the chart.
    let start: Date
    let end: Date

    @ScaledMetric(relativeTo: .body) private var chartHeight: CGFloat = 220

    var body: some View {
        let floors = maxHeartRate?.zoneFloors ?? []
        let top = Double(max(maxHeartRate?.bpm ?? 0, samples.map(\.bpm).max() ?? 0))
        let low = Double(samples.map(\.bpm).min() ?? 0)
        let from = samples.first?.at ?? start
        let to = samples.last?.at ?? end

        Chart {
            ForEach(Array(floors.enumerated()), id: \.offset) { index, floor in
                RectangleMark(
                    xStart: .value("From", from), xEnd: .value("To", to),
                    yStart: .value("Floor", Double(floor)),
                    yEnd: .value("Ceiling", index + 1 < floors.count ? Double(floors[index + 1]) : top))
                    .foregroundStyle(ZoneColour.of(index + 1).opacity(0.22))
            }
            ForEach([start, end], id: \.self) { edge in
                RuleMark(x: .value("Session", edge))
                    .lineStyle(StrokeStyle(lineWidth: 1, dash: [3, 3]))
                    .foregroundStyle(Color(.secondaryLabel))
            }
            ForEach(Array(samples.enumerated()), id: \.offset) { _, sample in
                LineMark(x: .value("Time", sample.at), y: .value("bpm", sample.bpm))
                    .interpolationMethod(.monotone)
                    .lineStyle(StrokeStyle(lineWidth: 1.5))
                    .foregroundStyle(Color(.label))
            }
        }
        .chartYScale(domain: max(0, min(low, Double(floors.first ?? Int(low))) - 5)...(top + 5))
        .chartXAxis {
            AxisMarks(values: .automatic(desiredCount: 4)) { _ in
                AxisGridLine()
                AxisValueLabel(format: .dateTime.hour().minute())
            }
        }
        .frame(height: chartHeight)
        .padding(.vertical, 6)
    }
}

/// Minutes in each zone, zone 5 at the top, each with its colour and its floor in beats per minute,
/// and the minutes below zone 1 beneath them.
struct ZoneMinutesGrid: View {
    let zones: ZoneMinutes
    let maxHeartRate: MaxHeartRate?

    var body: some View {
        let floors = maxHeartRate?.zoneFloors ?? []
        Grid(alignment: .leading, horizontalSpacing: 12, verticalSpacing: 6) {
            ForEach(Array(zones.byZone.enumerated()).reversed(), id: \.offset) { index, minutes in
                GridRow {
                    RoundedRectangle(cornerRadius: 2).fill(ZoneColour.of(index + 1)).frame(width: 10, height: 10)
                    Text("Zone \(index + 1)").font(.subheadline)
                    Text(floors.count == 5 ? "\(floors[index])+ bpm" : "")
                        .font(.caption.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                    Text(Formatting.minutes(minutes))
                        .font(.subheadline.monospacedDigit())
                        .gridColumnAlignment(.trailing)
                }
                .accessibilityElement(children: .combine)
            }
            GridRow {
                Color.clear.frame(width: 10, height: 10)
                Text("Below zone 1").font(.subheadline).foregroundStyle(Color(.secondaryLabel))
                Text("")
                Text(Formatting.minutes(zones.belowZones))
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
        .padding(.vertical, 2)
    }
}
