import Charts
import SwiftUI

/// An injury's gait: walking asymmetry and double support in the eight weeks before it against the
/// four after, with the weeks around it drawn and the day marked.
///
/// The two figures that rise when a leg is hurt — the body shortens its time on the sore side and
/// keeps both feet down for longer — measured on every walk by the phone, so they say what the
/// injury did to walking, and whether it has come back, without anything logged by hand.
struct InjuryGaitSection: View {
    let injuredOn: CalendarDay

    @Environment(HealthMeasuresStore.self) private var store

    @ScaledMetric(relativeTo: .body) private var chartHeight: CGFloat = 140

    private static let figures: [HealthMetric] = [.walkingAsymmetry, .walkingDoubleSupport]

    var body: some View {
        let today = CalendarDay(Date())
        let readings = Self.figures.compactMap { metric in
            store.history.principal(metric).map { InjuryGait.of(metric, $0.days, injuredOn: injuredOn, today: today) }
        }.filter(\.hasReadings)

        if !readings.isEmpty {
            Section {
                ForEach(readings, id: \.metric) { gait in
                    row(gait)
                    if gait.around.count >= 2 {
                        chart(gait)
                    }
                }
            } header: {
                Text("Gait around the injury")
            } footer: {
                Text("From your iPhone's walks: the eight weeks before against the four from the injury. Both rise when you favour a leg.")
            }
            .task { await store.refreshIfNeeded() }
        }
    }

    private func row(_ gait: InjuryGait) -> some View {
        LabeledContent {
            VStack(alignment: .trailing, spacing: 2) {
                Text([gait.before, gait.after].map { $0.map(gait.metric.text) ?? "–" }.joined(separator: " → "))
                    .monospacedDigit()
                if let change = gait.change {
                    Text(gait.metric.change(change))
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(MeasureRow.colour(of: gait.metric.isImprovement(change)))
                }
            }
        } label: {
            VStack(alignment: .leading, spacing: 2) {
                Text(gait.metric.title)
                if let recent = gait.recent {
                    Text("Last 4 weeks \(gait.metric.text(recent))")
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
        }
        .accessibilityElement(children: .combine)
    }

    private func chart(_ gait: InjuryGait) -> some View {
        Chart {
            RuleMark(x: .value("Injury", injuredOn.middayDate()))
                .lineStyle(StrokeStyle(lineWidth: 1, dash: [3, 3]))
                .foregroundStyle(.orange)
            ForEach(gait.around) { day in
                PointMark(x: .value("Day", day.day.middayDate()), y: .value(gait.metric.title, gait.metric.shown(day.mean)))
                    .symbolSize(10)
                    .foregroundStyle(day.day < injuredOn ? Color(.tertiaryLabel) : Tokens.accent)
            }
        }
        .chartYScale(domain: .automatic(includesZero: false))
        .chartXAxis {
            AxisMarks(values: .automatic(desiredCount: 4)) { _ in
                AxisGridLine()
                AxisValueLabel(format: .dateTime.month(.abbreviated).day())
            }
        }
        .frame(height: chartHeight)
        .padding(.vertical, 4)
        .accessibilityLabel("\(gait.metric.title) around the injury")
        .accessibilityValue([gait.before.map { "before \(gait.metric.text($0))" }, gait.after.map { "after \(gait.metric.text($0))" }]
            .compactMap(\.self).joined(separator: ", "))
    }
}
