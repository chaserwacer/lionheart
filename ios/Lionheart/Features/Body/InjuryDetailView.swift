import Charts
import SwiftUI

/// One injury and everything logged against it, newest first.
///
/// The pain chart is the reading that matters: a tracked problem is a trajectory, and a list of
/// numbers makes the reader do the comparison themselves. Oldest-first in the chart, newest-first
/// in the list — the order each is actually read in.
struct InjuryDetailView: View {
    let history: InjuryHistory

    var body: some View {
        List {
            Section {
                LabeledContent("Since") { Text(Formatting.date(history.injury.injuryDate)) }
                LabeledContent("State") {
                    Text(history.injury.isActive ? "Active" : "Resolved")
                }
                if let pain = history.latestPain {
                    LabeledContent("Latest pain") {
                        Text("\(pain)/10").monospacedDigit()
                    }
                }
            }

            if let notes = history.injury.notes.nonEmpty {
                Section("Notes") { Text(notes) }
            }

            painSection
            eventsSection
        }
        .listStyle(.insetGrouped)
        .navigationTitle(history.injury.name.nonEmpty ?? "Injury")
        .navigationBarTitleDisplayMode(.inline)
    }

    @ViewBuilder
    private var painSection: some View {
        let dated = history.events.compactMap { event in
            event.createdAt.map { (event: event, at: $0) }
        }
        if dated.count >= 2 {
            Section {
                Chart(dated.sorted { $0.at < $1.at }, id: \.event.injuryEventId) { point in
                    LineMark(
                        x: .value("Logged", point.at),
                        y: .value("Pain", point.event.painLevel))
                    .foregroundStyle(Tokens.accent)
                    PointMark(
                        x: .value("Logged", point.at),
                        y: .value("Pain", point.event.painLevel))
                    .foregroundStyle(Tokens.accent)
                }
                .chartYScale(domain: 0...10)
                .frame(height: 160)
                .padding(.vertical, 8)
                .accessibilityLabel("Pain level over time, nought to ten")
            } header: {
                Text("Pain")
            } footer: {
                Text("Only events carrying a date are plotted.")
            }
        }
    }

    @ViewBuilder
    private var eventsSection: some View {
        Section("Events") {
            if history.events.isEmpty {
                Text("Nothing logged against this yet.")
                    .foregroundStyle(Color(.secondaryLabel))
            } else {
                ForEach(history.events) { event in
                    InjuryEventRow(event: event)
                }
            }
        }
    }
}

/// One treatment or check-in.
private struct InjuryEventRow: View {
    let event: InjuryEvent

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            LabeledContent {
                Text("\(event.painLevel)/10")
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            } label: {
                Label(kind, systemImage: systemImage)
                    .font(.subheadline)
            }
            if let createdAt = event.createdAt {
                Text(createdAt.formatted(.dateTime.month(.abbreviated).day().year()))
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
            }
            if let notes = event.notes.nonEmpty {
                Text(notes)
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }

    private var kind: String {
        switch event.kind {
        case .treatment: "Treatment"
        case .checkIn: "Check-in"
        case .unrecognised: "Event"
        }
    }

    private var systemImage: String {
        switch event.kind {
        case .treatment: "cross.case"
        case .checkIn: "checkmark.circle"
        case .unrecognised: "questionmark.circle"
        }
    }
}
