import SwiftUI

/// What the Add button offers: a lift from the catalogue, distance-and-time work, or a set in words.
///
/// One sheet for everything a session holds, because the athlete's question is "what am I doing
/// next", not "which kind of thing is it". It opens at half height on the priority lifts and the
/// recent ones, which is where the answer nearly always is, and grows to full height for a search.
/// Rows are buttons that hand back at once — no confirming, no second step.
struct AddWorkSheet: View {
    let library: MovementLibraryStore
    /// Each family's priority lift, so they lead the sheet.
    let priorityIds: [UUID]
    let pick: (AddedWork) -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var query = ""
    @State private var detent: PresentationDetent = .medium

    var body: some View {
        NavigationStack {
            // Built from whatever the catalogue holds, empty while it loads: distance and time and
            // words need no catalogue, and in a basement with nothing remembered they are still
            // there to log — only the movement sections wait on it.
            list(AddWorkMenu.of(
                catalogue: library.state.value ?? [],
                recentIds: library.recent.map(\.movementDataId),
                priorityIds: priorityIds,
                previous: library.previous,
                query: query))
            .navigationTitle("Add")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Cancel") { dismiss() }
                }
            }
            .searchable(
                text: $query,
                placement: .navigationBarDrawer(displayMode: .always),
                prompt: "Movements and cardio")
            // Typing wants the whole height for results; the keyboard would cover a half sheet.
            .onChange(of: query.isEmpty) { _, isEmpty in
                if !isEmpty { detent = .large }
            }
            .task { await library.refreshIfNeeded() }
        }
        .presentationDetents([.medium, .large], selection: $detent)
        .presentationDragIndicator(.visible)
    }

    @ViewBuilder
    private func list(_ menu: AddWorkMenu) -> some View {
        if menu.isEmpty, library.state.value != nil {
            ContentUnavailableView.search(text: query)
        } else {
            List {
                catalogueStatus
                if !menu.priorities.isEmpty {
                    Section("Priority lifts") {
                        ForEach(menu.priorities) { movementRow($0) }
                    }
                }
                if !menu.recent.isEmpty {
                    Section("Recent") {
                        ForEach(menu.recent.prefix(8)) { movementRow($0) }
                    }
                }
                if !menu.cardio.isEmpty || !menu.distanceMovements.isEmpty {
                    Section("Distance and time") {
                        ForEach(menu.cardio) { work in
                            choice(work.title, detail: nil) { finish(.cardio(work)) }
                        }
                        ForEach(menu.distanceMovements) { movementRow($0) }
                    }
                }
                if query.isEmpty {
                    Section {
                        choice("In words", detail: "Mobility, a class, anything unmeasured") { finish(.words) }
                    }
                }
                if !menu.movements.isEmpty {
                    Section(query.isEmpty ? "All movements" : "Movements") {
                        ForEach(menu.movements) { movementRow($0) }
                    }
                }
            }
            .listStyle(.insetGrouped)
            .listSectionSpacing(.compact)
        }
    }

    /// Where the catalogue stands when it has nothing to list yet: rows shaped like what is coming,
    /// or why it is not, with a retry worth trying.
    @ViewBuilder
    private var catalogueStatus: some View {
        switch library.state {
        case .loading:
            Section("Movements") { SkeletonRows(count: 3) }
        case .unavailable(let failure):
            Section("Movements") {
                VStack(alignment: .leading, spacing: 4) {
                    Text("Movements are not available")
                        .font(.subheadline.weight(.semibold))
                    Text(failure.message)
                        .font(.footnote)
                        .foregroundStyle(Color(.secondaryLabel))
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await library.refresh() } }
                            .font(.footnote)
                    }
                }
            }
        case .loaded, .stale:
            EmptyView()
        }
    }

    private func movementRow(_ movement: MovementData) -> some View {
        choice(movement.title, detail: movement.qualifier, trailing: lastTime(movement)) {
            finish(.movement(movement))
        }
    }

    /// The last working set done on it, the number the next one is aimed at.
    private func lastTime(_ movement: MovementData) -> String? {
        library.previous[movement.movementDataId]?.workingSets.first.flatMap(Formatting.lastTime)
    }

    private func choice(
        _ title: String, detail: String?, trailing: String? = nil, action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text(title)
                        .foregroundStyle(Color(.label))
                    if let detail, !detail.isEmpty {
                        Text(detail)
                            .font(.caption)
                            .foregroundStyle(Color(.secondaryLabel))
                    }
                }
                Spacer(minLength: 8)
                if let trailing {
                    Text(trailing)
                        .font(.caption.monospacedDigit())
                        .foregroundStyle(Color(.tertiaryLabel))
                        .accessibilityLabel("Last time \(trailing)")
                }
            }
            .contentShape(.rect)
        }
        .buttonStyle(.plain)
    }

    private func finish(_ work: AddedWork) {
        pick(work)
        dismiss()
    }
}

/// What the athlete picked to add.
enum AddedWork {
    case movement(MovementData)
    case cardio(CardioWork)
    case words
}
