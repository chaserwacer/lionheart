import Charts
import SwiftUI

/// The readings the athlete entered about themselves, newest first.
///
/// The overall score is the server's own average — with stress inverted, since a low stress score
/// is a good day — and is printed as received. Recomputing it here would give the two clients and
/// the analyses two different numbers for one reading.
struct WellnessHistoryView: View {
    let readings: [WellnessState]
    /// The tab's own reload. Passed in rather than holding the store: entries are written on the
    /// web app, so this screen reads and nothing more.
    let refresh: () async -> Void

    var body: some View {
        List {
            if readings.isEmpty {
                ContentUnavailableView {
                    Text("No wellness entries")
                } description: {
                    Text("A daily reading of mood, energy, motivation and stress gives Lionheart a signal your training does not carry.")
                }
                .listRowBackground(Color.clear)
            } else {
                overallSection
                Section("Entries") {
                    ForEach(readings) { state in
                        let reading = WellnessReading.of(state)
                        WellnessRow(reading: reading, today: today)
                            // The server has an add route and no update or delete, so copying is
                            // the only thing a logged entry can offer. Adding one is a form, and a
                            // form is not a menu item.
                            .contextMenu {
                                Button {
                                    Clipboard.copy(Self.summary(of: reading))
                                } label: {
                                    Label("Copy", systemImage: "doc.on.doc")
                                }
                            }
                    }
                }
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Wellness")
        .refreshable { await refresh() }
    }

    /// One reading as a line of text: the day, the overall figure, and the four behind it.
    static func summary(of reading: WellnessReading) -> String {
        let components = reading.components.map { "\($0.name) \($0.score)" }
        return ([
            Formatting.date(reading.day),
            "overall \(MeasurementText.plain(reading.overall))",
        ] + components).joined(separator: " · ")
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
