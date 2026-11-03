import Charts
import SwiftUI

/// The readings the athlete entered about themselves, newest first.
///
/// The overall score is the server's own average — with stress inverted, since a low stress score
/// is a good day — and is printed as received. Recomputing it here would give the two clients and
/// the analyses two different numbers for one reading.
struct WellnessHistoryView: View {
    let readings: [WellnessState]

    var body: some View {
        List {
            if readings.isEmpty {
                ContentUnavailableView {
                    Label("No wellness entries", systemImage: "face.smiling")
                } description: {
                    Text("A daily reading of mood, energy, motivation and stress gives the coach something your training does not say.")
                }
                .listRowBackground(Color.clear)
            } else {
                overallSection
                Section("Entries") {
                    ForEach(readings) { state in
                        WellnessRow(reading: WellnessReading.of(state), today: today)
                    }
                }
            }
        }
        .listStyle(.insetGrouped)
        .navigationTitle("Wellness")
    }

    @ViewBuilder
    private var overallSection: some View {
        if readings.count >= 2 {
            Section {
                // Oldest first so the line reads left to right, which is the opposite of the list
                // beneath it and the right way round for a chart.
                Chart(readings.sorted { $0.date < $1.date }) { state in
                    LineMark(
                        x: .value("Day", state.date.middayDate()),
                        y: .value("Overall", state.overallScore))
                    .foregroundStyle(Tokens.accent)
                    PointMark(
                        x: .value("Day", state.date.middayDate()),
                        y: .value("Overall", state.overallScore))
                    .foregroundStyle(Tokens.accent)
                }
                .chartYScale(domain: Double(WellnessReading.scale.lowerBound)...Double(WellnessReading.scale.upperBound))
                .frame(height: 160)
                .padding(.vertical, 8)
                .accessibilityLabel("Overall wellness over time, one to five")
            } header: {
                Text("Overall")
            } footer: {
                Text("The server's own average of the four, with stress inverted — a low stress score is a good day.")
            }
        }
    }

    private var today: CalendarDay { CalendarDay(Date()) }
}

/// One reading: the day, the overall figure, and the four components behind it.
private struct WellnessRow: View {
    let reading: WellnessReading
    let today: CalendarDay

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            LabeledContent {
                Text(MeasurementText.plain(reading.overall))
                    .font(.headline.monospacedDigit())
            } label: {
                Text(Formatting.day(RelativeDay.of(reading.day, today: today)))
                    .font(.headline)
            }

            // Top-aligned so the figures stay level when a label wraps at the larger Dynamic Type
            // sizes.
            HStack(alignment: .top, spacing: 12) {
                ForEach(reading.components) { component in
                    StatTile(value: "\(component.score)", label: component.name)
                }
            }
        }
        .padding(.vertical, 4)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(
            "\(Formatting.date(reading.day)), overall \(MeasurementText.plain(reading.overall))")
    }
}
