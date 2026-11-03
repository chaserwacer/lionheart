import SwiftUI

/// Every movement the log holds, most recently trained first, each opening its page.
///
/// The way to a movement that does not start from a session. Variants are listed apart, as the
/// catalogue holds them, since a paused squat's history is not a high-bar squat's.
struct MovementsView: View {
    let history: HistoryStore

    @State private var query = ""

    var body: some View {
        let index = history.state.value?.movements ?? MovementIndex(entries: [])
        let shown = index.matching(query)
        let today = CalendarDay(Date())

        List {
            if index.entries.isEmpty {
                ContentUnavailableView {
                    Text("No movements")
                } description: {
                    Text("Movements are listed once a completed session has performed them.")
                }
                .listRowBackground(Color.clear)
            } else if shown.isEmpty {
                ContentUnavailableView.search(text: query)
                    .listRowBackground(Color.clear)
            } else {
                Section {
                    ForEach(shown) { entry in
                        NavigationLink(value: HistoryRoute.movement(entry.movementDataId)) {
                            MovementIndexRow(entry: entry, today: today)
                        }
                    }
                } footer: {
                    Text("Most recently trained first.")
                }
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Movements")
        .searchable(text: $query, prompt: "Movement")
        .task { await history.refreshIfNeeded() }
    }
}

/// A movement's name over how many sessions it has and when it was last done. Two lines rather
/// than a trailing column, because the names run long and a column beside them wraps both.
private struct MovementIndexRow: View {
    let entry: MovementIndex.Entry
    let today: CalendarDay

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(entry.name)
            Text("\(Formatting.count(entry.sessionCount, "session")) · last \(Formatting.date(entry.lastDay, today: today))")
                .font(.subheadline.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
        }
        .accessibilityElement(children: .combine)
    }
}
