import SwiftUI

/// What the athlete is striving for this block: the one priority lift of push, pull and legs, each
/// with its progress and frequency, the week's cardio against its targets, and any movement pinned
/// for emphasis on its own.
///
/// Reached from Train, the score and History. The priorities are the server's reading
/// (`PriorityReport`), which the score reads too; the emphasis pins are read here against the log,
/// as they always have been.
struct PrioritiesView: View {
    let focus: FocusStore
    let history: HistoryStore
    let library: MovementLibraryStore

    @Environment(TrainingBalanceStore.self) private var balance
    @State private var editing: FocusPinSheet.Editing?
    @State private var deleting: FocusPin?

    var body: some View {
        let today = CalendarDay(Date())

        List {
            if case .stale(_, let staleness) = balance.priorities { StaleBanner(staleness) }
            ForEach(PatternFamily.judged, id: \.wire) { family in
                familySection(family)
            }
            cardioSection
            emphasisSections(today: today)
            if case .unavailable(let failure) = balance.priorities {
                Section {
                    Text(failure.message).foregroundStyle(Color(.secondaryLabel))
                }
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Priorities")
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Menu("Pin", systemImage: "plus") {
                    ForEach(PatternFamily.judged, id: \.wire) { family in
                        Button("\(family.name) priority") {
                            editing = .prioritising(family, standing: focus.priority(for: family))
                        }
                    }
                    Button("Movement for emphasis") { editing = .pinning(movementDataId: nil, name: nil) }
                }
            }
        }
        .refreshable {
            async let pins: Void = focus.refresh()
            async let read: Void = balance.refresh()
            _ = await (pins, read)
        }
        .task {
            async let pins: Void = focus.refreshIfNeeded()
            async let read: Void = balance.refreshIfNeeded()
            async let log: Void = history.refreshIfNeeded()
            await library.refreshIfNeeded()
            _ = await (pins, read, log)
        }
        .sheet(item: $editing, onDismiss: { Task { await balance.refresh() } }) { editing in
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
                Task {
                    await focus.delete(pin)
                    await balance.refresh()
                }
            }
        } message: { _ in
            Text("For a pin made by mistake. Unpinning keeps it as a past pin; deleting removes it.")
        }
        .alert(
            "Refused",
            isPresented: Binding(get: { focus.action.refusal != nil }, set: { if !$0 { focus.dismissRefusal() } })
        ) {
            Button("OK") { focus.dismissRefusal() }
        } message: {
            if let refusal = focus.action.refusal { Text(refusal.message) }
        }
    }

    // MARK: - Families

    @ViewBuilder
    private func familySection(_ family: PatternFamily) -> some View {
        let standing = focus.priority(for: family)
        Section {
            switch balance.priorities {
            case .loading:
                SkeletonRows(count: 1)
            case .unavailable:
                Text("Not available").foregroundStyle(Color(.secondaryLabel))
            case .loaded(let report), .stale(let report, _):
                let lift = report.lift(for: family)
                if case .chosen(let chosen) = lift {
                    NavigationLink(value: HistoryRoute.movement(chosen.movementDataId)) {
                        PriorityProgressRow(family: family, lift: lift, showsSparkline: true)
                    }
                } else {
                    PriorityProgressRow(family: family, lift: lift)
                }
            }
        } footer: {
            HStack(spacing: 16) {
                Button(standing == nil ? "Set priority" : "Change") {
                    editing = .prioritising(family, standing: standing)
                }
                if let standing {
                    Button("Edit") { editing = .revising(standing) }
                    Button("Unpin") {
                        Task {
                            await focus.unpin(standing)
                            await balance.refresh()
                        }
                    }
                }
                Spacer()
            }
            .font(.footnote)
            .disabled(focus.action.isRunning)
        }
    }

    // MARK: - Cardio

    @ViewBuilder
    private var cardioSection: some View {
        if let report = balance.priorities.value {
            Section {
                switch report.cardio {
                case .measured(let zone2, let quality, _, _, _):
                    if let part = balance.score.value?.cardio, !part.zones.isEmpty {
                        ForEach(ScoreBreakdown.zoneRows(part)) { ScoreCellRow(cell: $0) }
                    } else {
                        LabeledContent(ZoneBand.zone2.name) { Text("\(Formatting.measure(zone2.rounded())) min").monospacedDigit() }
                        LabeledContent(ZoneBand.quality.name) { Text("\(Formatting.measure(quality.rounded())) min").monospacedDigit() }
                    }
                case .unknown(let reason):
                    Text(CardioWords.unknown(reason)).foregroundStyle(Color(.secondaryLabel))
                case .unrecognised:
                    Text("Not readable by this version.").foregroundStyle(Color(.secondaryLabel))
                }
            } header: {
                Text("Cardio, last 7 days")
            } footer: {
                Text("Zone 2 (60–70 % of maximum heart rate) and quality work — bouts of two minutes or more at 80 % and above in runs, rides and other endurance sessions — each against this week's target, grown from the four weeks before toward 150 and 40 minutes. From cardio sessions only; the ring's own detections only where their heart rate showed training, and never its games or dances.")
            }
        }
    }

    // MARK: - Emphasis and past pins

    @ViewBuilder
    private func emphasisSections(today: CalendarDay) -> some View {
        let pins = focus.state.value ?? []
        let snapshot = history.state.value
        let catalogue = snapshot?.catalogue ?? library.catalogue
        let readings = pins.map { pin in
            FocusReading.of(
                pin,
                history: snapshot?.history(of: pin.movementDataId, today: today)
                    ?? MovementHistory(movementDataId: pin.movementDataId, sessions: []),
                today: today)
        }
        let emphasis = readings.filter { $0.pin.role.priorityFamily == nil }.arrangedForReading()
        let pastPriorities = readings.filter { $0.pin.role.priorityFamily != nil && !$0.pin.isPinned }
            .arrangedForReading().past

        if !emphasis.current.isEmpty {
            Section("Emphasis") {
                ForEach(emphasis.current) { reading in row(reading, name: catalogue.name(for: reading.pin.movementDataId)) }
            }
        }
        if !pastPriorities.isEmpty || !emphasis.past.isEmpty {
            Section("Past") {
                ForEach(pastPriorities + emphasis.past) { reading in
                    row(reading, name: pastName(reading, catalogue: catalogue))
                }
            }
        }
    }

    private func row(_ reading: FocusReading, name: String) -> some View {
        NavigationLink(value: HistoryRoute.movement(reading.pin.movementDataId)) {
            FocusSummaryRow(reading: reading, name: name)
        }
        .contextMenu {
            if reading.pin.isPinned {
                Button("Edit", systemImage: "slider.horizontal.3") { editing = .revising(reading.pin) }
                Button("Unpin", systemImage: "pin.slash") { Task { await focus.unpin(reading.pin) } }
            }
            Button("Delete", systemImage: "trash", role: .destructive) { deleting = reading.pin }
        }
    }

    /// A past priority says which family it led.
    private func pastName(_ reading: FocusReading, catalogue: MovementCatalogue) -> String {
        let name = catalogue.name(for: reading.pin.movementDataId)
        guard let family = reading.pin.role.priorityFamily else { return name }
        return "\(family.name) · \(name)"
    }
}
