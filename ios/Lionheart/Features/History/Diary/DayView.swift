import SwiftUI

/// One day of the diary: how the body was, what was trained, the cardio, and his own rating of it.
///
/// Read top to bottom the way the day went: the night and Oura's scores for the morning, the one
/// line of what the ring measured, then every workout — lifting from the log, the ring's workouts
/// from the cardio report — then the day's movement and his wellness rating. Every workout opens:
/// a session onto its sets, a ring workout onto its heart-rate curve.
///
/// The day steps back and forward from the toolbar, as Today's does, so the diary reads day after
/// day without going back to the log.
struct DayView: View {
    let history: HistoryStore
    let diary: DiaryStore
    let library: MovementLibraryStore

    @State private var day: CalendarDay

    init(day: CalendarDay, history: HistoryStore, diary: DiaryStore, library: MovementLibraryStore) {
        self.history = history
        self.diary = diary
        self.library = library
        _day = State(initialValue: day)
    }

    var body: some View {
        let today = CalendarDay(Date())
        let sessions = (history.state.value?.sessions ?? []).filter { $0.date == day }

        List {
            switch diary.days[day] ?? .loading {
            case .loading:
                Section { SkeletonRows(count: 3) }
                trainingSection(sessions, reading: nil, today: today)
            case .unavailable(let failure):
                Section {
                    Text(failure.message).foregroundStyle(Color(.secondaryLabel))
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await diary.loadDay(day) } }
                    }
                } header: {
                    Text("Ring and cardio")
                }
                trainingSection(sessions, reading: nil, today: today)
            case .loaded(let reading):
                content(reading, sessions: sessions, today: today, staleBecause: nil)
            case .stale(let reading, let staleness):
                content(reading, sessions: sessions, today: today, staleBecause: staleness)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle(title(today: today))
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItemGroup(placement: .topBarTrailing) {
                Button("Previous day", systemImage: "chevron.left") { day = day.adding(days: -1) }
                Button("Next day", systemImage: "chevron.right") { day = day.adding(days: 1) }
                    .disabled(day >= today)
            }
        }
        .refreshable { await diary.loadDay(day) }
        .task(id: day) {
            if diary.days[day]?.value == nil { await diary.loadDay(day) }
            await history.refreshIfNeeded()
        }
    }

    private func title(today: CalendarDay) -> String {
        switch RelativeDay.of(day, today: today) {
        case .today: "Today"
        case .yesterday: "Yesterday"
        case .tomorrow, .daysAgo, .inDays, .distant: Formatting.date(day)
        }
    }

    @ViewBuilder
    private func content(
        _ reading: DiaryDay, sessions: [TrainingSession], today: CalendarDay, staleBecause staleness: Staleness?
    ) -> some View {
        if let staleness { StaleBanner(staleness) }
        bodySection(reading)
        trainingSection(sessions, reading: reading, today: today)
        cardioSection(reading)
        wellnessSection(reading)
        weekSection
    }

    // MARK: - Sections

    /// Oura's scores for the day, the measured line under them, and the night they came from.
    @ViewBuilder
    private func bodySection(_ reading: DiaryDay) -> some View {
        let scores: [(label: String, value: String)] = [
            reading.ouraDay?.readiness?.score.map { (label: "Readiness", value: $0.description) },
            reading.ouraDay?.sleep?.score.map { (label: "Sleep score", value: $0.description) },
        ].compactMap(\.self)
        let measured = reading.recovery.map { figure in
            (label: Formatting.metricName(figure.metric), value: Formatting.figureText(figure.value, of: figure.metric))
        }
        let steps = reading.ouraDay?.activity.map(\.steps)

        Section {
            if scores.isEmpty, measured.isEmpty, steps == nil {
                Text("The ring recorded nothing for this day.").foregroundStyle(Color(.secondaryLabel))
            }
            if !scores.isEmpty {
                LabeledFigures(caption: "Oura's scores, out of 100", figures: scores)
            }
            if !measured.isEmpty || steps != nil {
                LabeledFigures(
                    caption: "Measured",
                    figures: measured + (steps.map { [(label: "Steps", value: $0.formatted())] } ?? []))
            }
            if let night = reading.night {
                NavigationLink(value: HistoryRoute.night(day)) {
                    NightSummaryRow(night: night)
                }
            }
            NavigationLink(value: HistoryRoute.dayActivity(day)) {
                Text("Movement through the day")
            }
        } header: {
            Text("Body")
        } footer: {
            Text("Readiness and sleep score are Oura's ratings; the measured figures are from the night's main sleep, and steps from the day.")
        }
    }

    @ViewBuilder
    private func trainingSection(_ sessions: [TrainingSession], reading: DiaryDay?, today: CalendarDay) -> some View {
        let catalogue = history.state.value?.catalogue ?? library.catalogue
        Section("Training") {
            if sessions.isEmpty {
                Text("Nothing logged.").foregroundStyle(Color(.secondaryLabel))
            }
            ForEach(sessions) { session in
                NavigationLink(value: HistoryRoute.session(session.id)) {
                    VStack(alignment: .leading, spacing: 5) {
                        TrainingDayRow(session: session)
                        if !session.sets.isEmpty {
                            MovementLines(digests: MovementDigest.of(session.sets, catalogue: catalogue))
                        }
                        if let cardio = reading?.cardio(of: session.id) {
                            CardioFacts(session: cardio)
                        }
                    }
                }
            }
        }
    }

    @ViewBuilder
    private func cardioSection(_ reading: DiaryDay) -> some View {
        let workouts = reading.ringWorkouts
        if !workouts.isEmpty {
            Section {
                ForEach(workouts) { session in
                    NavigationLink(value: HistoryRoute.cardioSession(session.id, on: day)) {
                        CardioSessionRow(session: session)
                    }
                }
            } header: {
                Text("Workouts the ring recorded")
            } footer: {
                Text("As the cardio report counts them: walks under 30 minutes, and workouts inside a logged session, are left out.")
            }
        }
    }

    @ViewBuilder
    private func wellnessSection(_ reading: DiaryDay) -> some View {
        if let state = reading.wellness {
            let wellness = WellnessReading.of(state)
            Section {
                LabeledFigures(
                    caption: "Your rating, 1 to 5",
                    figures: [(label: "Overall", value: MeasurementText.plain(wellness.overall))]
                        + wellness.components.map { (label: $0.name, value: "\($0.score)") })
            } header: {
                Text("Wellness")
            } footer: {
                Text("Overall is the mean of the four, with stress counted the other way round.")
            }
        }
    }

    @ViewBuilder
    private var weekSection: some View {
        if let monday = TrainingTrends.weekStart(of: day) {
            Section {
                NavigationLink(value: HistoryRoute.week(monday)) {
                    Text("Week of \(Formatting.date(monday))")
                }
            }
        }
    }
}

/// A caption over a row of figures, each a label and its value.
private struct LabeledFigures: View {
    let caption: String
    let figures: [(label: String, value: String)]

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(caption)
                .font(.caption)
                .foregroundStyle(Color(.secondaryLabel))
            SummaryFigures(figures: figures)
        }
        .padding(.vertical, 2)
    }
}

/// A logged session's cardio as one line: its heart rate and its zones, where the ring has them.
struct CardioFacts: View {
    let session: CardioSession

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            switch session.heartRate {
            case .recorded(let average, let peak, _, let zones):
                Text("Heart rate avg \(average) · peak \(peak) bpm\(zones.map { " · Z2 \(Formatting.minutes($0.zone2))" } ?? "")")
                    .font(.footnote.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
                if let zones, zones.inZones > 0 {
                    ZoneBar(zones: zones).frame(height: 6)
                }
            case .notRecorded:
                Text("No heart rate synced for this session.")
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
            case .unrecognised:
                EmptyView()
            }
        }
    }
}
