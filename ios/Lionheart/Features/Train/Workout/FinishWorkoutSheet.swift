import SwiftUI

/// Finishing a workout: what it came to, how it felt, and the one button that hands it over.
///
/// It is the last chance to say anything about the session. The effort ratings and the notes may
/// already have been given during it, from the logging surface's "How it's going", and they open
/// here as given, so finishing never asks twice. A rating changed here is written to the workout
/// straight away and the notes when the sheet closes, so "Not yet" keeps both.
///
/// Every rating may be left alone. The server treats an effort with nothing in it as effort not
/// reported, which is a different fact from effort rated low, and the sheet does not nag.
struct FinishWorkoutSheet: View {
    let store: ActiveWorkoutStore
    let library: MovementLibraryStore
    let bodyweight: BodyweightStore
    let routines: RoutineStore
    /// Receives the readout of the finished workout, built before it is handed over.
    let finished: (WorkoutSummary) -> Void

    @Environment(\.dismiss) private var dismiss

    @State private var notes: String
    @State private var endedAt: Date
    @State private var startedAt: Date

    init(
        store: ActiveWorkoutStore, library: MovementLibraryStore, bodyweight: BodyweightStore,
        routines: RoutineStore, finished: @escaping (WorkoutSummary) -> Void
    ) {
        self.store = store
        self.library = library
        self.bodyweight = bodyweight
        self.routines = routines
        self.finished = finished
        _notes = State(initialValue: store.workout?.notes ?? "")
        _startedAt = State(initialValue: store.workout?.startedAt ?? Date())
        _endedAt = State(initialValue: Self.proposedEnd(for: store.workout))
    }

    /// When the session is to be recorded as having ended.
    ///
    /// Now, for a live workout. For one being written down afterwards, an hour after the start the
    /// athlete typed — a plausible length they can adjust, rather than "now", which would record a
    /// session that ran from yesterday morning until this evening.
    private static func proposedEnd(for workout: ActiveWorkout?) -> Date {
        guard let workout else { return Date() }
        switch workout.destination {
        case .liveSession, .deferredStart: return Date()
        case .afterTheFact: return workout.startedAt.addingTimeInterval(3600)
        }
    }

    var body: some View {
        NavigationStack {
            Form {
                if let workout = store.workout {
                    summarySection(workout)
                    timeSection(workout)
                    // Inside, because the ratings are the workout's: drawn after it has gone, on
                    // the way out from a finish, they would flash back to unrated.
                    effortSection
                }
                notesSection
                if case .refused(let failure) = store.finish { refusalSection(failure) }
            }
            .navigationTitle("Finish workout")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Not yet") { dismiss() }
                }
                ToolbarItem(placement: .topBarTrailing) {
                    finishButton
                }
            }
        }
        // After a finish the workout is gone and this changes nothing.
        .onDisappear { store.note(notes) }
    }

    // MARK: - Sections

    private func summarySection(_ workout: ActiveWorkout) -> some View {
        let totals = WorkoutTotals.of(workout.sets)
        return Section {
            LabeledContent("Working sets") { Text("\(totals.setCount)").monospacedDigit() }
            if let pounds = totals.poundsVolume {
                LabeledContent("Volume") {
                    Text(Formatting.volume(pounds, .pounds)).monospacedDigit()
                }
            }
            ForEach(totals.distances) { distance in
                LabeledContent("Distance") {
                    Text(Formatting.distance(distance.distance, distance.unit)).monospacedDigit()
                }
            }
            if totals.movingDuration > 0 {
                LabeledContent("Moving time") {
                    Text(Formatting.duration(seconds: totals.movingDuration)).monospacedDigit()
                }
            }
        } header: {
            Text("What you did")
        } footer: {
            if workout.sets.contains(where: { !$0.command.isWork }) {
                Text("Warm-ups are not counted.")
            }
        }
    }

    @ViewBuilder
    private func timeSection(_ workout: ActiveWorkout) -> some View {
        switch workout.destination {
        case .liveSession, .deferredStart:
            Section("When") {
                LabeledContent("Started") { Text(workout.startedAt, style: .time) }
                LabeledContent("Length") {
                    Text(Formatting.duration(seconds: max(0, endedAt.timeIntervalSince(workout.startedAt))))
                        .monospacedDigit()
                }
            }
        case .afterTheFact:
            Section {
                DatePicker("Started", selection: $startedAt)
                DatePicker("Finished", selection: $endedAt)
            } header: {
                Text("When")
            } footer: {
                // The server reads the calendar day off the start, at the offset it is sent with,
                // so the day this session belongs to is the day of the start the athlete sets here.
                Text("The session is recorded on the day it started.")
            }
        }
    }

    private var effortSection: some View {
        Section {
            EffortRatingRows(store: store)
        } header: {
            Text("How it felt")
        } footer: {
            Text("Leave these alone and the session is recorded with no effort reported.")
        }
    }

    private var notesSection: some View {
        Section("Notes") {
            TextField("Anything worth remembering", text: $notes, axis: .vertical)
                .lineLimit(2...6)
        }
    }

    private func refusalSection(_ failure: LoadFailure) -> some View {
        Section {
            VStack(alignment: .leading, spacing: 4) {
                Label("Not finished", systemImage: "exclamationmark.triangle.fill")
                    .font(.subheadline.weight(.semibold))
                    .foregroundStyle(.orange)
                Text(failure.message)
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
                Text("Your sets are still saved on this phone. Nothing has been lost.")
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
    }

    private var finishButton: some View {
        Button {
            Task { await finish() }
        } label: {
            if case .finishing = store.finish {
                ProgressView()
            } else {
                Text("Finish").fontWeight(.semibold)
            }
        }
        .disabled(isFinishing || store.workout == nil)
    }

    private var isFinishing: Bool {
        if case .finishing = store.finish { return true }
        return false
    }

    private func finish() async {
        guard let workout = store.workout else { return }

        store.note(notes)
        if case .afterTheFact = workout.destination {
            // The athlete typed when it started, so the workout's own start has to move with it —
            // the period sent is built from it.
            store.moveStart(to: startedAt)
        }

        // Read before the hand-over: once the server has the session, last time on every movement
        // is this workout, and the readout would compare it with itself.
        let summary = WorkoutSummary.of(
            store.workout ?? workout,
            endingAt: endedAt,
            previous: library.previous,
            records: library.records,
            setups: library.setups,
            bodyweight: bodyweight.series,
            lastRoutineSession: workout.routine.flatMap { routines.routine($0.routineId)?.lastSession })

        switch await store.complete(endingAt: endedAt) {
        case .delivered:
            Haptics.achievement()
            finished(summary.completed(as: workout.destination.sessionId))
            dismiss()
            // Completing is what establishes records, and the library's copy is what the next
            // workout's set rows are judged against and what the picker shows as the number to aim
            // at. Left alone it would hold the records as they stood at launch. The routines are
            // read again for the same reason: this session is now the routine's last.
            await library.refresh()
            if workout.routine != nil { await routines.refresh() }
        case .heldForDelivery:
            // Finished as far as the athlete is concerned; Train lists it until the server has it.
            // The records cannot have moved yet, so there is nothing for the library to learn.
            Haptics.achievement()
            finished(summary)
            dismiss()
        case .notFinished:
            break
        }
    }
}
