import SwiftUI

/// One session, opened: what was done, how long it took, and what the athlete said about it.
///
/// It takes the session the list already holds rather than re-reading it by id. The window read is
/// one request that answered the whole screen, and asking the server again for a record the device
/// is already showing would be a round trip to learn nothing.
///
/// The sections differ by lifecycle state because the states carry different facts — there is no
/// "duration: —" row on a planned session, because a planned session has no duration to be missing.
///
/// Each movement's header opens the movement's page, and a set that established a record carries
/// the mark, read from the records the server says this session set (`RecordMarks`).
struct SessionDetailView: View {
    let session: TrainingSession
    let catalogue: MovementCatalogue
    /// Where the session's established records are read and held.
    let history: HistoryStore

    private var marks: RecordMarks {
        let records = history.established[.session(session.id)]?.value ?? []
        return RecordMarks.of(records, in: [RecordMarks.Session(
            sessionId: session.id,
            startedAt: session.startedAt,
            sets: session.sets.enumerated().map { SessionDetail.IndexedSet(order: $0.offset, entry: $0.element) })])
    }

    var body: some View {
        let marks = marks

        List {
            headerSection(marks)

            switch session {
            case .completed(let completed):
                performed(completed.sets, marks: marks)
                periodSection(completed.period)
                effortSection(completed.effort)
                decorationSection(completed.decoration)
            case .active(let active):
                performed(active.sets, marks: marks)
                Section("Started") {
                    LabeledContent("At") { Text(active.startedAt, style: .time) }
                }
            case .planned(let planned):
                prescriptions(planned.prescriptions)
            case .skipped:
                Section {
                    Text("Skipped")
                        .foregroundStyle(Color(.secondaryLabel))
                } footer: {
                    Text("A skipped session is kept on purpose — not training is part of the record.")
                }
            case .unrecognised(let type, _):
                Section {
                    Text("This version of Lionheart cannot read a \(type.nonEmpty ?? "session") of this kind")
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }

            notesSection
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        // Lets a set row be as short as its one line; every other row here is taller anyway.
        .environment(\.defaultMinListRowHeight, 34)
        .navigationTitle(Formatting.date(session.date))
        .navigationBarTitleDisplayMode(.inline)
        .task(id: session.id) {
            guard case .completed = session else { return }
            await history.loadEstablished(by: session)
        }
    }

    // MARK: - Sections

    /// The state, without the day: the navigation title already states it — then what the sets came
    /// to, and how many of them set a record.
    private func headerSection(_ marks: RecordMarks) -> some View {
        Section {
            LabeledContent("State", value: SessionLog.kind(of: session, today: CalendarDay(Date())).label)
            if !session.sets.isEmpty {
                LabeledContent("Total") {
                    Text(Formatting.totals(WorkoutTotals.of(entries: session.sets))).monospacedDigit()
                }
            }
            let recordSets = marks.markedSetCount(in: session.id)
            let sessionVolumes = marks.sessionVolumeCount(in: session.id)
            if recordSets > 0 || sessionVolumes > 0 {
                LabeledContent("Records") {
                    Text(Formatting.recordSummary(sets: recordSets, sessionVolumes: sessionVolumes))
                        .multilineTextAlignment(.trailing)
                }
            }
            if session.envelope.provenance == .aiAssisted {
                // Worth saying: a session the model helped structure reads differently from one the
                // athlete typed out, and provenance is orthogonal to lifecycle on purpose.
                LabeledContent("Logged") { Text("With help from the model") }
            }
        }
    }

    @ViewBuilder
    private func performed(_ sets: [SetEntry], marks: RecordMarks) -> some View {
        let detail = SessionDetail.of(sets, catalogue: catalogue)

        if detail.isEmpty {
            Section("Sets") {
                Text("No sets logged.").foregroundStyle(Color(.secondaryLabel))
            }
        } else {
            ForEach(detail.blocks) { block in
                let numbers = SetKind.numbers(of: block.sets.map(\.entry.kind))
                Section {
                    ForEach(Array(block.sets.enumerated()), id: \.element.id) { position, set in
                        SetRow(
                            number: numbers[position],
                            entry: set.entry,
                            record: marks.outcome(of: .init(sessionId: session.id, order: set.order)))
                    }
                } header: {
                    MovementHeader(movementDataId: block.movementDataId, name: block.movementName)
                } footer: {
                    if marks.setSessionVolume(in: session.id, on: .movement(block.movementDataId)) {
                        Text("Best session volume on this movement.")
                    }
                }
            }
            if !detail.unattached.isEmpty {
                Section("Other work") {
                    ForEach(detail.unattached) { set in
                        SetRow(number: set.order + 1, entry: set.entry)
                    }
                }
            }
        }
    }

    @ViewBuilder
    private func prescriptions(_ prescriptions: [SetPrescription]) -> some View {
        Section("Prescribed") {
            if prescriptions.isEmpty {
                Text("Nothing prescribed yet.").foregroundStyle(Color(.secondaryLabel))
            } else {
                ForEach(Array(prescriptions.enumerated()), id: \.offset) { index, prescription in
                    if case .freeform(let text) = prescription {
                        // A freeform prescription has no movement, and its words *are* the
                        // prescription — so they are the row, not the value beside a label
                        // reading "Unspecified".
                        Text(text.nonEmpty ?? "As described")
                            .accessibilityLabel("Prescription \(index + 1)")
                    } else {
                        LabeledContent {
                            Text(target(of: prescription)).monospacedDigit()
                        } label: {
                            Text(catalogue.name(for: prescription.movementDataId))
                        }
                        .accessibilityLabel("Prescription \(index + 1)")
                    }
                }
            }
        }
    }

    @ViewBuilder
    private func periodSection(_ period: Period) -> some View {
        Section("When") {
            switch period {
            case .instant(let at):
                LabeledContent("At") { Text(at, style: .time) }
            case .interval(let start, let end):
                LabeledContent("Start") { Text(start, style: .time) }
                LabeledContent("End") { Text(end, style: .time) }
                LabeledContent("Duration") {
                    Text(Formatting.duration(seconds: end.timeIntervalSince(start)))
                        .monospacedDigit()
                }
            case .unrecognised:
                Text("The period on this session could not be read.")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
    }

    @ViewBuilder
    private func effortSection(_ effort: EffortState) -> some View {
        Section("Perceived effort") {
            switch effort {
            case .notReported, .unrecognised:
                Text("Not rated.").foregroundStyle(Color(.secondaryLabel))
            case .reported(let ratings):
                if ratings.isEmpty {
                    Text("Not rated.").foregroundStyle(Color(.secondaryLabel))
                } else {
                    rating("Fatigue going in", ratings.accumulatedFatigue)
                    rating("Difficulty", ratings.difficultyRating)
                    rating("Engagement", ratings.engagementRating)
                    rating("Outside factors", ratings.externalVariablesRating)
                }
            }
        }
    }

    @ViewBuilder
    private func rating(_ label: String, _ value: Int?) -> some View {
        if let value {
            LabeledContent(label) { Text("\(value)").monospacedDigit() }
        }
    }

    @ViewBuilder
    private func decorationSection(_ decoration: DecorationState) -> some View {
        switch decoration {
        case .decorated(let facets, let freeTags, _) where !facets.isEmpty || !freeTags.isEmpty:
            Section {
                ForEach(Array(facets.enumerated()), id: \.offset) { _, facet in
                    LabeledContent(Formatting.facetAxis(facet.axis)) { Text(facet.value) }
                }
                if !freeTags.isEmpty {
                    LabeledContent("Tags") { Text(freeTags.joined(separator: ", ")) }
                }
            } header: {
                Text("How Lionheart read it")
            } footer: {
                Text("Facets come from a fixed vocabulary, so analyses can count them.")
            }
        case .none, .decorated, .unrecognised:
            // Sessions are decorated on completion, so one just finished legitimately has none.
            // An empty section would say something is missing when nothing is.
            EmptyView()
        }
    }

    @ViewBuilder
    private var notesSection: some View {
        if let notes = session.envelope.notes.nonEmpty {
            Section("Notes") {
                Text(notes)
            }
        }
    }

    private func target(of prescription: SetPrescription) -> String {
        switch prescription {
        case .lift(let lift):
            "\(MeasurementText.plain(lift.targetWeight)) \(lift.unit.abbreviation) × \(lift.targetReps)"
        case .distanceTime(let target):
            "\(MeasurementText.plain(target.targetDistance)) \(target.unit.abbreviation) in \(Formatting.clock(seconds: target.targetDuration))"
        case .freeform(let text):
            text.nonEmpty ?? "As described"
        case .unrecognised:
            "Not readable"
        }
    }
}
