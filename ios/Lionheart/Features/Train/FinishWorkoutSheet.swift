import SwiftUI

/// Finishing a workout: what it came to, how it felt, and the one button that hands it over.
///
/// It is the last chance to say anything about the session, so the effort ratings and the notes
/// live here rather than on the logging surface — a screen the athlete is on for an hour should not
/// be carrying a form they fill in once.
///
/// Every rating may be left alone. The server treats an effort with nothing in it as effort not
/// reported, which is a different fact from effort rated low, and the sheet does not nag.
struct FinishWorkoutSheet: View {
    let store: ActiveWorkoutStore
    let library: MovementLibraryStore

    /// The scale the self-reported ratings use.
    ///
    /// One to five, matching `WellnessState`'s four component scores, which is the only other thing
    /// in Lionheart the athlete rates about themselves. The server stores these as plain integers
    /// and validates no range, so the scale is the client's to be consistent about.
    private static let scale = 1...5

    @Environment(\.dismiss) private var dismiss

    @State private var fatigue: Int?
    @State private var difficulty: Int?
    @State private var engagement: Int?
    @State private var externalVariables: Int?
    @State private var notes: String
    @State private var endedAt: Date
    @State private var startedAt: Date

    init(store: ActiveWorkoutStore, library: MovementLibraryStore) {
        self.store = store
        self.library = library
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
        case .liveSession: return Date()
        case .afterTheFact: return workout.startedAt.addingTimeInterval(3600)
        }
    }

    var body: some View {
        NavigationStack {
            Form {
                if let workout = store.workout {
                    summarySection(workout)
                    timeSection(workout)
                }
                effortSection
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
    }

    // MARK: - Sections

    private func summarySection(_ workout: ActiveWorkout) -> some View {
        let totals = WorkoutTotals.of(workout.sets)
        return Section("What you did") {
            LabeledContent("Sets") { Text("\(totals.setCount)").monospacedDigit() }
            ForEach(totals.weights) { weight in
                LabeledContent("Volume") {
                    Text(Formatting.volume(weight)).monospacedDigit()
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
        }
    }

    @ViewBuilder
    private func timeSection(_ workout: ActiveWorkout) -> some View {
        switch workout.destination {
        case .liveSession:
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
            rating("Accumulated fatigue", selection: $fatigue)
            rating("Difficulty", selection: $difficulty)
            rating("Engagement", selection: $engagement)
            rating("Outside factors", selection: $externalVariables)
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

    /// One rating row. A `Picker` in a `Form` renders as Apple's own value row, which gets Dynamic
    /// Type and VoiceOver right and makes "not rated" an option rather than a thing to undo.
    private func rating(_ label: String, selection: Binding<Int?>) -> some View {
        Picker(label, selection: selection) {
            Text("Not rated").tag(Int?.none)
            ForEach(Self.scale, id: \.self) { value in
                Text("\(value)").tag(Int?.some(value))
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

        let effort = PerceivedEffortRequest(
            accumulatedFatigue: fatigue,
            difficultyRating: difficulty,
            engagementRating: engagement,
            externalVariablesRating: externalVariables)

        if await store.complete(effort: effort, endingAt: endedAt) {
            Haptics.achievement()
            dismiss()
            // Completing is what establishes records, and the library's copy is what the next
            // workout's set rows are judged against and what the picker shows as the number to aim
            // at. Left alone it would hold the records as they stood at launch.
            await library.refresh()
        }
    }
}
