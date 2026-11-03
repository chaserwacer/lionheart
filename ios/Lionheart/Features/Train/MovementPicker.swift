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
                    // The shape of the list that is coming rather than a spinner in the middle of
                    // an empty sheet: this sheet is opened mid-workout and the wait is a round trip.
                    List { Section("Movements") { SkeletonRows(count: 8) } }
                        .listStyle(.insetGrouped)
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
                        ForEach(library.recent) { movement in
                            row(movement)
                                // Only on the recent list, because that is the only list this can
                                // take a movement out of. Swiping it off the catalogue would read
                                // as deleting the movement, which this cannot do and should not.
                                .swipeActions(edge: .trailing) {
                                    Button(role: .destructive) {
                                        library.forgetUsed(movement.movementDataId)
                                    } label: {
                                        Label("Forget", systemImage: "clock.badge.xmark")
                                    }
                                }
                                .contextMenu { menu(for: movement, isRecent: true) }
                        }
                    }
                }
                Section(query.isEmpty ? "All movements" : "Results") {
                    ForEach(matches) { movement in
                        row(movement)
                            .contextMenu { menu(for: movement, isRecent: false) }
                    }
                }
            }
            .listStyle(.insetGrouped)
            .refreshable { await library.refresh() }
        }
    }

    /// The long press.
    ///
    /// There is nothing here that changes a movement: the library is curated on the web app,
    /// because a movement needs an equipment and a base chosen or invented first. What the device
    /// does own is which movements are *recent*, and the record standing against one is the number
    /// worth having on the pasteboard when the athlete is working out what to aim at.
    @ViewBuilder
    private func menu(for movement: MovementData, isRecent: Bool) -> some View {
        Button {
            select(movement)
            dismiss()
        } label: {
            Label("Log a set of this", systemImage: "plus.circle")
        }
        Button {
            Clipboard.copy(copyText(for: movement))
        } label: {
            Label("Copy", systemImage: "doc.on.doc")
        }
        if isRecent {
            Button(role: .destructive) {
                library.forgetUsed(movement.movementDataId)
            } label: {
                Label("Forget this one", systemImage: "clock.badge.xmark")
            }
        }
    }

    private func copyText(for movement: MovementData) -> String {
        guard let heaviest = heaviestSet(on: movement) else { return movement.title }
        return "\(movement.title) · best \(Formatting.weight(heaviest.weight, heaviest.unit))"
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
                if let heaviest = heaviestSet(on: movement) {
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

    /// The heaviest set standing in the unit the athlete lifted this movement in most recently —
    /// the number they are about to aim at. One unit only: the other unit's record is a different
    /// record, and printing the larger figure would be the wrong number half the time.
    private func heaviestSet(on movement: MovementData) -> Achievement.Lift? {
        let lifting = library.records(on: movement.movementDataId).map(RecordReading.of)?.lifting
        guard case .heaviestSet(let lift) = lifting?.first?.heaviestSet?.achievement else { return nil }
        return lift
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
