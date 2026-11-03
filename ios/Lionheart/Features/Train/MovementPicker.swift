import SwiftUI

/// The exercise library, as a sheet that hands back one movement.
///
/// Recently-used first, with no typing: mid-workout the movement wanted is almost always one of
/// the last few, and making the athlete search for the bench press they were on two minutes ago is
/// the kind of friction that gets an app put down. `.searchable` is there for the other times.
///
/// There is no way to create a movement here. A movement needs an equipment and a base chosen or
/// invented first, which is catalogue curation rather than training; the web app does it, and an
/// empty library says so rather than offering half a form.
struct MovementPicker: View {
    let library: MovementLibraryStore
    let select: (MovementData) -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var query = ""

    var body: some View {
        NavigationStack {
            Group {
                switch library.state {
                case .loading:
                    ProgressView().controlSize(.large)
                case .unavailable(let failure):
                    unavailable(failure)
                case .loaded(let movements), .stale(let movements, _):
                    list(movements)
                }
            }
            .navigationTitle("Movements")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Cancel") { dismiss() }
                }
            }
            .searchable(text: $query, prompt: "Search movements")
            .task { await library.refreshIfNeeded() }
        }
    }

    @ViewBuilder
    private func list(_ movements: [MovementData]) -> some View {
        let matches = movements.filter { $0.matches(query) }

        if movements.isEmpty {
            ContentUnavailableView {
                Label("No movements yet", systemImage: "dumbbell")
            } description: {
                Text("Movements are built from equipment and an exercise on the Lionheart web app. Add a few there and they will appear here.")
            }
        } else if matches.isEmpty {
            ContentUnavailableView.search(text: query)
        } else {
            List {
                if query.isEmpty, !library.recent.isEmpty {
                    Section("Recent") {
                        ForEach(library.recent) { row($0) }
                    }
                }
                Section(query.isEmpty ? "All movements" : "Results") {
                    ForEach(matches) { row($0) }
                }
            }
            .listStyle(.insetGrouped)
        }
    }

    private func row(_ movement: MovementData) -> some View {
        Button {
            select(movement)
            dismiss()
        } label: {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text(movement.title)
                        .foregroundStyle(Color(.label))
                    Text(movement.qualifier)
                        .font(.caption)
                        .foregroundStyle(Color(.secondaryLabel))
                }
                Spacer(minLength: 8)
                if let records = library.records(for: movement.movementDataId),
                   let heaviest = records.strength {
                    // The standing record, where there is one: it is the number the athlete is
                    // about to aim at. Shown with its unit, because a record's unit is the
                    // athlete's and a bare figure is the wrong number half the time.
                    let stated = "\(Formatting.measure(heaviest.weight)) \(heaviest.unit.abbreviation)"
                    Text(stated)
                        .font(.caption.monospacedDigit())
                        .foregroundStyle(Color(.tertiaryLabel))
                        .accessibilityLabel("Record \(stated)")
                }
            }
            .contentShape(.rect)
        }
        .buttonStyle(.plain)
    }

    @ViewBuilder
    private func unavailable(_ failure: LoadFailure) -> some View {
        ContentUnavailableView {
            Label("Movements are not available", systemImage: "exclamationmark.triangle")
        } description: {
            Text(failure.message)
        } actions: {
            if failure.isWorthRetrying {
                Button("Try again") { Task { await library.refresh() } }
            }
        }
    }
}
