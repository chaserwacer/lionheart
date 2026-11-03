import SwiftUI

/// The numbers, by movement — and by trail running, which names no movement.
///
/// One row per subject rather than one per record: a movement with a rep-max grid holds a dozen
/// records or more, and a flat list of every one of them buries the question this screen answers,
/// which is "where do I stand on the bench". Each row carries the subject's headline figure and
/// opens into its records.
struct PersonalRecordsView: View {
    let records: [SubjectRecords]
    let catalogue: MovementCatalogue

    @State private var query = ""

    var body: some View {
        List {
            if records.isEmpty {
                ContentUnavailableView {
                    Label("No records yet", systemImage: "trophy")
                } description: {
                    Text("A record appears here when a completed session beats your best — a heavier set, a new rep max, a longer or faster run.")
                }
                .listRowBackground(Color.clear)
            } else if shown.isEmpty {
                ContentUnavailableView.search(text: query)
                    .listRowBackground(Color.clear)
            } else {
                Section {
                    ForEach(shown) { held in
                        NavigationLink(value: HistoryRoute.recordSubject(held.subject)) {
                            SubjectRow(held: held, name: held.subject.name(in: catalogue))
                        }
                    }
                } footer: {
                    Text("Most recently set first.")
                }
            }
        }
        .listStyle(.insetGrouped)
        .navigationTitle("Personal records")
        .searchable(text: $query, prompt: "Movement")
    }

    /// The subjects whose name matches the search, in the server's order — most recently set first.
    private var shown: [SubjectRecords] {
        guard let wanted = query.nonEmpty else { return records }
        return records.filter { $0.subject.name(in: catalogue).localizedStandardContains(wanted) }
    }
}

/// One subject: its name, its headline figure, and how much it holds.
private struct SubjectRow: View {
    let held: SubjectRecords
    let name: String

    var body: some View {
        let reading = RecordReading.of(held)

        HStack(alignment: .firstTextBaseline) {
            VStack(alignment: .leading, spacing: 3) {
                Text(name)
                    .font(.headline)
                if let headline = reading.headline {
                    Text("\(headline.achievement.kind.label) \(Formatting.achievement(headline.achievement))")
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                }
                if let last = held.lastAchievedAt {
                    Text("Last set \(Formatting.recordDate(last))")
                        .font(.caption)
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
            Spacer(minLength: 8)
            Text("\(held.records.count)")
                .font(.subheadline.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
                .accessibilityLabel(Formatting.count(held.records.count, "record"))
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }
}
