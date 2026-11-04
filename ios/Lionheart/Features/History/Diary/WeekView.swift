import SwiftUI

/// One Monday-to-Sunday week: the training and the cardio it held, beside how the body was across
/// it, and the seven days to open.
///
/// The training comes first because the week is a unit of training — sessions a week is the
/// athlete's first goal — then the ring's week in means, so a heavy week and a low HRV sit on one
/// screen. Lifting is the log's sets and volume by session; cardio is the server's minutes by
/// heart-rate zone.
struct WeekView: View {
    let history: HistoryStore
    let diary: DiaryStore

    @State private var start: CalendarDay

    init(weekStarting start: CalendarDay, history: HistoryStore, diary: DiaryStore) {
        self.history = history
        self.diary = diary
        _start = State(initialValue: start)
    }

    var body: some View {
        let today = CalendarDay(Date())
        let training = WeekTraining.of(history.state.value?.sessions ?? [], weekStarting: start, today: today)

        List {
            trainingSection(training)
            switch diary.weeks[start] ?? .loading {
            case .loading:
                Section("Cardio") { SkeletonRows(count: 2) }
                Section("Body") { SkeletonRows(count: 3) }
            case .unavailable(let failure):
                Section {
                    Text(failure.message).foregroundStyle(Color(.secondaryLabel))
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await diary.loadWeek(start) } }
                    }
                } header: {
                    Text("Cardio and body")
                }
            case .loaded(let week):
                content(week, today: today, staleBecause: nil)
            case .stale(let week, let staleness):
                content(week, today: today, staleBecause: staleness)
            }
            daysSection(today: today)
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle(Formatting.week(startingOn: start))
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItemGroup(placement: .topBarTrailing) {
                Button("Previous week", systemImage: "chevron.left") { start = start.adding(days: -7) }
                Button("Next week", systemImage: "chevron.right") { start = start.adding(days: 7) }
                    .disabled(start.adding(days: 7) > today)
            }
        }
        .refreshable { await diary.loadWeek(start) }
        .task(id: start) {
            if diary.weeks[start]?.value == nil { await diary.loadWeek(start) }
            await history.refreshIfNeeded()
        }
    }

    @ViewBuilder
    private func content(_ week: DiaryWeek, today: CalendarDay, staleBecause staleness: Staleness?) -> some View {
        if let staleness { StaleBanner(staleness) }
        cardioSection(week)
        bodySection(week, today: today)
    }

    // MARK: - Logged training

    private func trainingSection(_ training: WeekTraining) -> some View {
        Section {
            if training.isEmpty {
                Text("No sessions logged.").foregroundStyle(Color(.secondaryLabel))
            } else {
                SummaryFigures(figures: [
                    (label: "Sessions", value: "\(training.sessions.count)"),
                    (label: "Sets", value: "\(training.totals.setCount)"),
                ] + training.totals.weights.filter { $0.volume > 0 }.map {
                    (label: "Volume, \($0.unit.abbreviation)", value: Int($0.volume.rounded()).formatted())
                })
                .padding(.vertical, 2)
                ForEach(training.sessions) { session in
                    NavigationLink(value: HistoryRoute.session(session.sessionId)) {
                        LabeledContent {
                            Text(Formatting.totals(session.totals)).monospacedDigit()
                        } label: {
                            VStack(alignment: .leading, spacing: 1) {
                                Text(session.day.middayDate().formatted(.dateTime.weekday(.wide)))
                                if let duration = session.duration {
                                    Text(Formatting.duration(seconds: duration))
                                        .font(.caption.monospacedDigit())
                                        .foregroundStyle(Color(.secondaryLabel))
                                }
                            }
                        }
                    }
                }
            }
        } header: {
            Text("Logged training")
        } footer: {
            Text("Every session logged, lifting and distance work. Sets leave warm-ups out; volume is weight × reps, totalled in each unit lifted in.")
        }
    }

    // MARK: - Cardio

    @ViewBuilder
    private func cardioSection(_ week: DiaryWeek) -> some View {
        Section {
            if let tally = week.cardio, tally.sessions > 0 {
                SummaryFigures(figures: [
                    (label: "Sessions", value: "\(tally.sessions)"),
                    (label: "Minutes", value: Int(tally.minutes.rounded()).formatted()),
                    (label: "Zone 2 min", value: Int(tally.zones.zone2.rounded()).formatted()),
                ])
                .padding(.vertical, 2)
                if tally.zones.inZones > 0 {
                    ZoneMinutesRow(zones: tally.zones, maxHeartRate: week.maxHeartRate)
                }
                ForEach(week.cardioSessions) { session in
                    NavigationLink(value: HistoryRoute.cardioSession(session.id, on: CalendarDay(session.start))) {
                        CardioSessionRow(session: session)
                    }
                }
            } else {
                Text("No cardio counted this week.").foregroundStyle(Color(.secondaryLabel))
            }
        } header: {
            Text("Cardio")
        } footer: {
            Text("Minutes in each heart-rate zone, from the ring's all-day heart rate across each session. Zones are shares of maximum heart rate; Body › Cardio says how the maximum is set.")
        }
    }

    // MARK: - Body

    @ViewBuilder
    private func bodySection(_ week: DiaryWeek, today: CalendarDay) -> some View {
        let means = week.means(today: today)
        Section {
            if means.isEmpty {
                Text("The ring recorded nothing this week.").foregroundStyle(Color(.secondaryLabel))
            }
            ForEach(means, id: \.metric) { mean in
                LabeledContent {
                    Text(Formatting.figureText(mean.value, of: mean.metric)).monospacedDigit()
                } label: {
                    VStack(alignment: .leading, spacing: 1) {
                        Text(mean.metric.isOuraScore
                             ? "\(Formatting.metricName(mean.metric)) (Oura)"
                             : Formatting.metricName(mean.metric))
                        Text(Formatting.count(mean.days, "day"))
                            .font(.caption.monospacedDigit())
                            .foregroundStyle(Color(.secondaryLabel))
                    }
                }
            }
            if let wellness = week.wellnessMean {
                LabeledContent {
                    Text(MeasurementText.plain((wellness * 100).rounded() / 100)).monospacedDigit()
                } label: {
                    VStack(alignment: .leading, spacing: 1) {
                        Text("Your wellness rating")
                        Text("\(Formatting.count(week.wellness.count, "day")), out of 5")
                            .font(.caption.monospacedDigit())
                            .foregroundStyle(Color(.secondaryLabel))
                    }
                }
            }
        } header: {
            Text("Body, mean of the week")
        } footer: {
            Text("Each is the mean over the days that carried it. Readiness and sleep score are Oura's ratings out of 100; the rest are measured. Oura's activity score is left out: it reads in the high nineties whatever the week held.")
        }
    }

    // MARK: - Days

    private func daysSection(today: CalendarDay) -> some View {
        let sessions = history.state.value?.sessions ?? []
        let catalogue = history.state.value?.catalogue ?? MovementCatalogue()
        let readiness = diary.weeks[start]?.value?.ouraDays
        return Section("Days") {
            ForEach((0..<7).map { start.adding(days: $0) }.filter { $0 <= today }, id: \.self) { day in
                let onDay = sessions.filter { $0.date == day && SessionLog.kind(of: $0, today: today).isPerformed }
                NavigationLink(value: HistoryRoute.day(day)) {
                    VStack(alignment: .leading, spacing: 2) {
                        HStack(alignment: .firstTextBaseline) {
                            Text(day.middayDate().formatted(.dateTime.weekday(.wide).day().month(.abbreviated)))
                                .font(.subheadline.weight(onDay.isEmpty ? .regular : .semibold))
                            Spacer()
                            if let score = readiness?.first(where: { $0.date == day })?.readiness?.score {
                                Text("Oura readiness \(score.description)")
                                    .font(.caption.monospacedDigit())
                                    .foregroundStyle(Color(.secondaryLabel))
                            }
                        }
                        Text(Self.line(sessions: onDay, ringWorkouts: ringWorkouts(on: day), catalogue: catalogue))
                            .font(.footnote)
                            .foregroundStyle(Color(.secondaryLabel))
                            .lineLimit(2)
                    }
                }
            }
        }
    }
}

extension WeekView {
    /// The ring's own workouts on a day of this week, as the cardio report counted them.
    fileprivate func ringWorkouts(on day: CalendarDay) -> Int {
        (diary.weeks[start]?.value?.cardioSessions ?? []).filter { session in
            guard CalendarDay(session.start) == day else { return false }
            if case .ringWorkout = session.source { return true }
            return false
        }.count
    }

    /// A day's line in the week: what was lifted, or that nothing was logged, and the ring's workouts.
    fileprivate static func line(sessions: [TrainingSession], ringWorkouts: Int, catalogue: MovementCatalogue) -> String {
        let names = sessions.flatMap { SessionLog.movementNames(in: $0, catalogue: catalogue) }
        let lifted = names.isEmpty ? "Nothing logged" : names.joined(separator: ", ")
        return ringWorkouts == 0 ? lifted : "\(lifted) · \(Formatting.count(ringWorkouts, "ring workout"))"
    }
}

/// A span's minutes in each zone: the stacked bar, and each zone's minutes beneath it with its
/// floor in beats per minute.
private struct ZoneMinutesRow: View {
    let zones: ZoneMinutes
    let maxHeartRate: MaxHeartRate?

    @ScaledMetric(relativeTo: .caption) private var columnWidth: CGFloat = 60

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            ZoneBar(zones: zones).frame(height: 8)
            LazyVGrid(columns: [GridItem(.adaptive(minimum: columnWidth), alignment: .leading)], alignment: .leading, spacing: 4) {
                ForEach(1...5, id: \.self) { zone in
                    VStack(alignment: .leading, spacing: 0) {
                        HStack(spacing: 4) {
                            RoundedRectangle(cornerRadius: 2).fill(ZoneColour.of(zone)).frame(width: 8, height: 8)
                            Text("Z\(zone)\(floor(of: zone).map { " \($0)+" } ?? "")")
                        }
                        .font(.caption2.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                        Text(Formatting.minutes(zones.byZone[zone - 1]))
                            .font(.caption.monospacedDigit())
                    }
                }
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }

    private func floor(of zone: Int) -> Int? {
        let floors = maxHeartRate?.zoneFloors ?? []
        return floors.count == 5 ? floors[zone - 1] : nil
    }
}
