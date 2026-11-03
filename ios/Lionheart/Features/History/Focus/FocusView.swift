import SwiftUI

/// The movements pinned for emphasis, each read against its horizon: how often it has been trained
/// against what the pin asked for, how it has moved since, the last time it was done, and how far
/// the target is.
///
/// It lives in History because every figure on it is a reading of the log — what has been done,
/// against what was meant — and the movements it names open the same pages the log does. The pins
/// being worked on lead; the ones unpinned or past their horizon follow as the record of what was
/// emphasised and for how long.
struct FocusView: View {
    let focus: FocusStore
    let history: HistoryStore
    let library: MovementLibraryStore

    @State private var editing: FocusPinSheet.Editing?
    @State private var deleting: FocusPin?

    var body: some View {
        let today = CalendarDay(Date())

        List {
            switch focus.state {
            case .loading:
                Section { SkeletonRows(count: 3) }
            case .unavailable(let failure):
                ContentUnavailableView {
                    Text("Focus is not available")
                } description: {
                    Text(failure.message)
                } actions: {
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await focus.refresh() } }
                    }
                }
                .listRowBackground(Color.clear)
            case .loaded(let pins):
                content(pins, today: today)
            case .stale(let pins, let staleness):
                StaleBanner(staleness)
                content(pins, today: today)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .environment(\.defaultMinListRowHeight, 34)
        .navigationTitle("Focus")
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button("Pin a movement", systemImage: "plus") { editing = .pinning(movementDataId: nil, name: nil) }
            }
        }
        .refreshable { await focus.refresh() }
        .task {
            await focus.refreshIfNeeded()
            await history.refreshIfNeeded()
        }
        .sheet(item: $editing) { editing in
            FocusPinSheet(editing: editing, focus: focus, library: library)
        }
        .confirmationDialog(
            "Delete this pin?",
            isPresented: Binding(get: { deleting != nil }, set: { if !$0 { deleting = nil } }),
            titleVisibility: .visible,
            presenting: deleting
        ) { pin in
            Button("Delete pin", role: .destructive) {
                deleting = nil
                Task { await focus.delete(pin) }
            }
        } message: { _ in
            Text("For a pin made by mistake. Unpinning keeps it as a past focus; deleting removes it.")
        }
        .alert(
            "Refused",
            isPresented: Binding(
                get: { focus.action.refusal != nil },
                set: { if !$0 { focus.dismissRefusal() } })
        ) {
            Button("OK") { focus.dismissRefusal() }
        } message: {
            if let refusal = focus.action.refusal { Text(refusal.message) }
        }
    }

    @ViewBuilder
    private func content(_ pins: [FocusPin], today: CalendarDay) -> some View {
        let snapshot = history.state.value
        let catalogue = snapshot?.catalogue ?? library.catalogue
        let readings = pins.map { pin in
            FocusReading.of(
                pin,
                history: snapshot?.history(of: pin.movementDataId, today: today)
                    ?? MovementHistory(movementDataId: pin.movementDataId, sessions: []),
                today: today)
        }
        let arranged = readings.arrangedForReading()

        if pins.isEmpty {
            ContentUnavailableView {
                Text("Nothing pinned")
            } description: {
                Text("Pin a movement to emphasise it over weeks, months or a year: how often it is trained, how it moves, and how far its target is.")
            } actions: {
                Button("Pin a movement") { editing = .pinning(movementDataId: nil, name: nil) }
            }
            .listRowBackground(Color.clear)
        }

        ForEach(arranged.current) { reading in
            pinSection(reading, name: catalogue.name(for: reading.pin.movementDataId))
        }

        if !arranged.past.isEmpty {
            Section {
                ForEach(arranged.past) { reading in
                    NavigationLink(value: HistoryRoute.movement(reading.pin.movementDataId)) {
                        FocusSummaryRow(reading: reading, name: catalogue.name(for: reading.pin.movementDataId))
                    }
                    .contextMenu { menu(for: reading.pin) }
                }
            } header: {
                Text("Past")
            }
        }
    }

    private func pinSection(_ reading: FocusReading, name: String) -> some View {
        Section {
            FocusReadingRows(reading: reading)
        } header: {
            MovementHeader(movementDataId: reading.pin.movementDataId, name: name)
                .contextMenu { menu(for: reading.pin) }
        } footer: {
            HStack(spacing: 16) {
                Button("Edit") { editing = .revising(reading.pin) }
                if reading.pin.isPinned {
                    Button("Unpin") { Task { await focus.unpin(reading.pin) } }
                }
                Spacer()
            }
            .font(.footnote)
            .disabled(focus.action.isRunning)
        }
    }

    @ViewBuilder
    private func menu(for pin: FocusPin) -> some View {
        if pin.isPinned {
            Button("Edit", systemImage: "slider.horizontal.3") { editing = .revising(pin) }
            Button("Unpin", systemImage: "pin.slash") { Task { await focus.unpin(pin) } }
        }
        Button("Delete", systemImage: "trash", role: .destructive) { deleting = pin }
    }
}
