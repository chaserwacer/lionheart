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
                    Text("No records")
                } description: {
                    Text("Records are set by completed sessions: a heavier set, a new rep max, a longer or faster run.")
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
        .listSectionSpacing(.compact)
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

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    var body: some View {
        let reading = RecordReading.of(held)
        // Side by side until the accessibility sizes, where a name and a figure sharing one line
        // would each wrap into a column of single words.
        let line = dynamicTypeSize.isAccessibilitySize
            ? AnyLayout(VStackLayout(alignment: .leading, spacing: 2))
            : AnyLayout(HStackLayout(alignment: .firstTextBaseline, spacing: 8))

        // The figure is the row's primary content, so it takes the trailing column at the weight
        // of the name; what kind of record it is, when, and how many the subject holds are the
        // quieter line under both.
        VStack(alignment: .leading, spacing: 2) {
            line {
                Text(name)
                    .font(.headline)
                    .frame(maxWidth: .infinity, alignment: .leading)
                if let headline = reading.headline {
                    Text(Formatting.achievement(headline.achievement))
                        .font(.headline.monospacedDigit())
                }
            }
            Text(detail(reading))
                .font(.subheadline.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
        }
        .accessibilityElement(children: .combine)
    }

    /// The headline's kind, the day the subject last set anything, and how many records it holds.
    private func detail(_ reading: RecordReading) -> String {
        [
            reading.headline?.achievement.kind.label,
            held.lastAchievedAt.map(Formatting.recordDate),
            Formatting.count(held.records.count, "record"),
        ]
        .compactMap(\.self)
        .joined(separator: " · ")
    }
}
