import SwiftUI

/// One day's movement: the timeline of what Oura classified each five minutes as, the long sittings
/// in it, and the training and sleep at either end.
///
/// The day is Oura's activity day, 04:00 to 04:00 on the athlete's clock, which is why the night
/// before and the night after can both appear on it.
struct DayActivityView: View {
    let day: CalendarDay
    let store: HabitsStore

    @ScaledMetric(relativeTo: .body) private var timelineHeight: CGFloat = 210

    var body: some View {
        List {
            switch store.days[day] ?? .loading {
            case .loading:
                Section { SkeletonRows(count: 4) }
            case .unavailable(let failure):
                ContentUnavailableView {
                    Text("Movement not available")
                } description: {
                    Text(failure.message)
                } actions: {
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await store.loadDay(day) } }
                    }
                }
                .listRowBackground(Color.clear)
            case .loaded(let detail):
                content(detail, staleBecause: nil)
            case .stale(let detail, let staleness):
                content(detail, staleBecause: staleness)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle(Formatting.date(day))
        .navigationBarTitleDisplayMode(.inline)
        .refreshable { await store.loadDay(day) }
        .task(id: day) {
            if store.days[day]?.value == nil { await store.loadDay(day) }
        }
    }

    @ViewBuilder
    private func content(_ detail: HabitsDayDetail?, staleBecause staleness: Staleness?) -> some View {
        if let staleness { StaleBanner(staleness) }

        if let detail {
            Section {
                FigureGrid(figures: Self.figures(detail.figures))
            } footer: {
                if !detail.figures.isComplete {
                    Text("More than three hours of this day are off the finger or lying still outside a scored sleep, so it is left out of the habit summaries.")
                }
            }
            if detail.runs.isEmpty {
                Section {
                    Text("No five-minute classification is stored for this day, so it has no shape to draw. Days synced before it was kept get one from the next sync.")
                        .foregroundStyle(Color(.secondaryLabel))
                }
            } else {
                timelineSection(detail)
                sittingsSection(detail)
            }
            if !detail.training.isEmpty || !detail.sleep.isEmpty {
                spansSection(detail)
            }
        } else {
            ContentUnavailableView {
                Text("No activity for this day")
            } description: {
                Text("Oura filed no activity document for it.")
            }
            .listRowBackground(Color.clear)
        }
    }

    static func figures(_ day: HabitsDay) -> [OuraPanel.Figure] {
        [
            OuraPanel.Figure(label: "Steps", value: day.steps.formatted()),
            OuraPanel.Figure(label: "Active", value: Formatting.minutes(Double(day.activeMinutes))),
            OuraPanel.Figure(label: "Sedentary", value: Formatting.minutes(Double(day.sedentaryMinutes))),
            day.longestSittingMinutes.map {
                OuraPanel.Figure(label: "Longest sitting", value: Formatting.minutes(Double($0)))
            },
            OuraPanel.Figure(label: "Sittings ≥ 1 h", value: "\(day.longSittings)"),
        ].compactMap(\.self)
    }

    // MARK: - Timeline

    private func timelineSection(_ detail: HabitsDayDetail) -> some View {
        Section {
            ActivityTimelineChart(detail: detail, isCompact: false)
                .frame(height: timelineHeight)
                .padding(.vertical, 6)
        } header: {
            Text("Through the day")
        } footer: {
            Text("Oura's class for every five minutes, 04:00 to 04:00. Shaded: sittings of an hour or more.")
        }
    }

    // MARK: - Sittings

    @ViewBuilder
    private func sittingsSection(_ detail: HabitsDayDetail) -> some View {
        Section {
            if detail.sittings.isEmpty {
                Text("No sitting of half an hour or more.").foregroundStyle(Color(.secondaryLabel))
            }
            ForEach(detail.sittings.sorted { $0.minutes > $1.minutes }, id: \.start) { sitting in
                LabeledContent {
                    Text(Formatting.minutes(Double(sitting.minutes)))
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(sitting.minutes >= Sitting.longMinutes ? Color(.label) : Color(.secondaryLabel))
                } label: {
                    Text("\(sitting.start.formatted(date: .omitted, time: .shortened))–\(sitting.end.formatted(date: .omitted, time: .shortened))")
                        .font(.subheadline.monospacedDigit())
                }
            }
        } header: {
            Text("Sittings, longest first")
        } footer: {
            Text("Unbroken waking stillness of 30 minutes or more. A stand-up shorter than five minutes may not register, so each is at most this long.")
        }
    }

    private func spansSection(_ detail: HabitsDayDetail) -> some View {
        Section("Training and sleep") {
            ForEach(Array(detail.training.enumerated()), id: \.offset) { _, session in
                LabeledContent {
                    Text("\(session.start.formatted(date: .omitted, time: .shortened))–\(session.end.formatted(date: .omitted, time: .shortened))")
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                } label: {
                    Text(TrainingSpanName.of(session))
                }
            }
            ForEach(Array(detail.sleep.enumerated()), id: \.offset) { _, sleep in
                LabeledContent {
                    // The weekday, because the day holds the end of one night and the start of the
                    // next, and two bare time ranges would read as two sleeps in one night.
                    Text("\(sleep.start.formatted(.dateTime.weekday(.abbreviated).hour().minute()))–\(sleep.end.formatted(date: .omitted, time: .shortened))")
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                } label: {
                    Text(sleep.isMainSleep ? "Sleep" : "Nap")
                }
            }
        }
    }
}

/// A training span's name as a reader says it.
enum TrainingSpanName {
    static func of(_ span: TrainingSpan) -> String {
        switch span.activity {
        case "strengthTraining" where span.isLogged: "Strength session"
        case "trailRun" where span.isLogged: "Trail run"
        case "ride", "indoorRide", "hike", "walk", "indoorRun" where span.isLogged:
            EnduranceActivity(wire: span.activity).title
        case "distanceAndTime" where span.isLogged: "Run"
        default: OuraVocabulary.phrase(span.activity)
        }
    }
}

/// A day's classified runs on the clock, in their lanes, with the long sittings shaded.
struct ActivityTimelineChart: View {
    let detail: HabitsDayDetail
    let isCompact: Bool

    var body: some View {
        LevelTimeline(
            lanes: ActivityLane.allCases,
            laneName: \.name,
            spans: detail.runs.compactMap { run in
                ActivityLane.of(run).map { .init(lane: $0, start: run.start, end: run.end, style: $0.colour) }
            },
            domain: detail.startsAt...max(detail.endsAt, detail.startsAt.addingTimeInterval(3600)),
            highlights: detail.sittings
                .filter { $0.minutes >= Sitting.longMinutes }
                .map { $0.start...$0.end },
            isCompact: isCompact)
            .accessibilityLabel("Movement through the day")
            .accessibilityValue(
                "Longest sitting \(detail.figures.longestSittingMinutes.map { Formatting.minutes(Double($0)) } ?? "none"), \(detail.figures.steps) steps")
    }
}

/// A day's movement in one row: the timeline as a strip, steps, and the longest sitting.
struct DayActivityRow: View {
    let detail: HabitsDayDetail

    @ScaledMetric(relativeTo: .body) private var stripHeight: CGFloat = 26

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            ViewThatFits(in: .horizontal) {
                HStack(alignment: .firstTextBaseline) {
                    steps
                    Spacer()
                    longest
                }
                VStack(alignment: .leading, spacing: 2) {
                    steps
                    longest
                }
            }
            ActivityTimelineChart(detail: detail, isCompact: true)
                .frame(height: stripHeight)
                .clipShape(.rect(cornerRadius: 3))
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }

    private var steps: some View {
        HStack(alignment: .firstTextBaseline, spacing: 6) {
            Text(detail.figures.steps.formatted())
                .font(.headline.monospacedDigit())
            Text("steps")
                .font(.subheadline)
                .foregroundStyle(Color(.secondaryLabel))
        }
        .fixedSize()
    }

    @ViewBuilder
    private var longest: some View {
        if let longest = detail.figures.longestSittingMinutes {
            Text("Longest sitting \(Formatting.minutes(Double(longest)))")
                .font(.subheadline.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
                .fixedSize()
        }
    }
}
