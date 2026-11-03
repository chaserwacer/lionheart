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

/// One pin in a line: its movement and where the horizon stands, then how often, how recently and
/// how far — for the head of History and the list of past pins.
struct FocusSummaryRow: View {
    let reading: FocusReading
    let name: String

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(alignment: .firstTextBaseline) {
                Text(name)
                    .font(.subheadline.weight(.semibold))
                    .lineLimit(2)
                Spacer(minLength: 8)
                Text(FocusWords.phase(reading.phase))
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
            Text(FocusWords.summary(reading))
                .font(.footnote.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
        }
        .padding(.vertical, 1)
        .accessibilityElement(children: .combine)
    }
}

/// A pin read in full, as labelled rows: horizon, sessions against the frequency, the estimated
/// single before and since, the target, and the last session.
struct FocusReadingRows: View {
    let reading: FocusReading
    /// Off on a movement's own page, whose summary already says when it was last done.
    var showsLastDone = true

    var body: some View {
        let attendance = reading.attendance

        LabeledContent("Horizon") {
            Text("\(FocusWords.horizon(reading.pin.horizon)) · \(FocusWords.phase(reading.phase))")
                .multilineTextAlignment(.trailing)
        }
        LabeledContent {
            Text(FocusWords.attendance(attendance))
                .monospacedDigit()
                .multilineTextAlignment(.trailing)
        } label: {
            VStack(alignment: .leading, spacing: 1) {
                Text("Sessions")
                if let perWeek = attendance.perWeek {
                    Text("\(Formatting.rate(perWeek)) a week against \(attendance.sessionsPerWeek)")
                        .font(.caption.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
        }
        if let progress = reading.progress {
            LabeledContent("Estimated 1RM") {
                Text(FocusWords.progress(progress))
                    .monospacedDigit()
                    .multilineTextAlignment(.trailing)
            }
        }
        if let target = FocusWords.target(reading.target) {
            LabeledContent("Target") {
                Text(target)
                    .monospacedDigit()
                    .multilineTextAlignment(.trailing)
            }
        }
        if showsLastDone {
            LabeledContent("Last done") {
                Text(reading.lastDone.map { Formatting.day(RelativeDay.of($0, today: CalendarDay(Date()))) } ?? "Never")
            }
        }
    }
}

/// The words a pin's reading is said in, written once for the summary line and the full rows.
enum FocusWords {
    static func horizon(_ horizon: FocusHorizon) -> String {
        let style = Date.FormatStyle.dateTime.month(.abbreviated).day()
        return "\(horizon.starts.middayDate().formatted(style)) – \(horizon.ends.middayDate().formatted(style.year()))"
    }

    static func phase(_ phase: FocusReading.Phase) -> String {
        switch phase {
        case .upcoming(let days): days == 1 ? "starts tomorrow" : "starts in \(days) days"
        case .running(let day, let of): "week \((day - 1) / 7 + 1) of \(Int((Double(of) / 7).rounded(.up)))"
        case .horizonOver: "horizon over"
        case .unpinned(let on): "unpinned \(on.middayDate().formatted(.dateTime.month(.abbreviated).day()))"
        case .unrecognised: ""
        }
    }

    static func attendance(_ attendance: FocusReading.Attendance) -> String {
        let expected = Int(attendance.expectedSoFar.rounded())
        return "\(attendance.sessions) of \(expected) so far · \(attendance.expectedOverHorizon) in all"
    }

    static func progress(_ progress: FocusReading.Progress) -> String {
        let unit = progress.unit
        let before = progress.before.map { Formatting.measure($0.value.rounded()) } ?? "—"
        let best = progress.best.map { Formatting.measure($0.value.rounded()) } ?? "—"
        let change = progress.change.map { " (\($0 < 0 ? "−" : "+")\(Formatting.measure(abs($0).rounded())))" } ?? ""
        return "\(before) → \(best) \(unit.abbreviation)\(change)"
    }

    static func target(_ target: FocusReading.TargetStanding) -> String? {
        switch target {
        case .untargeted, .unreadable:
            return nil
        case .met(let on, let weight, let reps):
            return "Met \(on.middayDate().formatted(.dateTime.month(.abbreviated).day())), \(Formatting.measure(weight)) × \(reps)"
        case .toGo(let weight, let unit, let reps, let best):
            let goal = "\(Formatting.weight(weight, unit)) × \(reps)"
            guard let best else { return "\(goal) · no set of \(reps) yet" }
            return "\(goal) · \(Formatting.weight(max(0, weight - best), unit)) to go"
        }
    }

    /// The footnote line: sessions against the frequency, the last session, the target.
    static func summary(_ reading: FocusReading) -> String {
        let attendance = reading.attendance
        var parts = ["\(attendance.sessions) of \(Int(attendance.expectedSoFar.rounded())) sessions"]
        if let last = reading.lastDone {
            parts.append("last \(inSentence(RelativeDay.of(last, today: CalendarDay(Date()))))")
        }
        switch reading.target {
        case .met: parts.append("target met")
        case .toGo(_, let unit, let reps, let best):
            parts.append(reading.target.remaining.map { "\(Formatting.weight($0, unit)) to target" }
                ?? (best == nil ? "no set of \(reps) yet" : ""))
        case .untargeted, .unreadable: break
        }
        return parts.filter { !$0.isEmpty }.joined(separator: " · ")
    }

    /// A day as it reads mid-sentence: "yesterday", "3 days ago", or the date.
    private static func inSentence(_ day: RelativeDay) -> String {
        switch day {
        case .today: "today"
        case .yesterday: "yesterday"
        case .daysAgo(let days): "\(days) days ago"
        case .tomorrow, .inDays, .distant: Formatting.day(day)
        }
    }
}
