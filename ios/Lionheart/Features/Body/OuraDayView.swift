import SwiftUI

/// One Oura day: the night, the shape of the day, and every score with the components behind it.
///
/// Each score is drawn as an instrument — its components against the athlete's own usual range —
/// rather than a grid of numbers, because a component is Oura's comparison with his baseline and
/// the comparison is the thing worth reading. Measurements that are not components (temperature,
/// steps, blood oxygen) sit under the score they belong to, as figures.
struct OuraDayView: View {
    let day: OuraDay
    /// The night's main sleep, with its clock time, where Oura scored one.
    let night: OuraEvent<OuraSleepPeriod>?
    /// The days around it, for each component's four-week baseline.
    let days: [OuraDay]
    let workouts: [OuraWorkout]
    let tags: [OuraTag]
    /// Whether to offer the day's movement timeline, which needs the day's activity stored.
    var opensDayActivity = true

    var body: some View {
        List {
            if let night {
                nightSection(night)
            }
            if opensDayActivity, day.activity != nil {
                Section {
                    NavigationLink(value: BodyRoute.dayActivity(day.date)) {
                        Text("Movement through the day")
                    }
                }
            }

            ContributorSection(
                group: .readiness, day: day, days: days,
                measurements: readinessMeasurements,
                explainsGauges: true)
            ContributorSection(group: .sleep, day: day, days: days)
            ContributorSection(
                group: .activity, day: day, days: days,
                measurements: day.activity.map(OuraPanel.activity).map(\.figures) ?? [])
            if let resilience = day.resilience {
                ContributorSection(
                    group: .resilience, day: day, days: days,
                    verdict: resilience.level.nonEmpty.map(OuraVocabulary.phrase))
            }

            ForEach(otherPanels) { panel in
                Section(panel.title) {
                    FigureGrid(figures: panel.figures)
                }
            }

            if !tags.isEmpty {
                tagSection
            }
            if !workouts.isEmpty {
                workoutSection
            }

            if day.isEmpty, night == nil {
                ContentUnavailableView {
                    Text("Nothing on this day")
                } description: {
                    Text("The ring filed nothing for it.")
                }
                .listRowBackground(Color.clear)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        // Lets a contributor row be as short as its one line, so a whole score reads on one screen.
        .environment(\.defaultMinListRowHeight, 34)
        .navigationTitle(Formatting.date(day.date))
        .navigationBarTitleDisplayMode(.inline)
    }

    /// Temperature is a measurement Oura reports beside readiness rather than a component of it.
    private var readinessMeasurements: [OuraPanel.Figure] {
        [
            RecoveryMetric.temperatureDeviation.value(day: day, night: nil).map {
                OuraPanel.Figure(label: "Temperature", value: Formatting.figureText($0, of: .temperatureDeviation))
            },
        ].compactMap(\.self)
    }

    /// The collections with measurements and no components.
    private var otherPanels: [OuraPanel] {
        OuraPanel.panels(of: day).filter { panel in
            switch panel {
            case .stress, .bloodOxygen, .vo2Max, .cardiovascularAge: true
            case .readiness, .sleep, .activity, .resilience: false
            }
        }
    }

    // MARK: - The night

    @ViewBuilder
    private func nightSection(_ night: OuraEvent<OuraSleepPeriod>) -> some View {
        Section {
            NavigationLink(value: BodyRoute.night(day.date)) {
                NightSummaryRow(night: night)
            }
        } header: {
            Text("The night")
        }
    }

    // MARK: - What the day carried

    private var tagSection: some View {
        Section {
            ForEach(Array(tags.enumerated()), id: \.offset) { _, tag in
                ForEach(tag.phrases, id: \.self) { phrase in
                    Text(phrase)
                }
            }
        } header: {
            Text("Tagged")
        } footer: {
            Text("Marked on the ring — the closest thing here to an account of the day in his own words.")
        }
    }

    private var workoutSection: some View {
        Section("Workouts the ring saw") {
            ForEach(Array(workouts.enumerated()), id: \.offset) { _, workout in
                LabeledContent {
                    Text(workout.summary)
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                } label: {
                    Text(workout.activityPhrase)
                }
            }
        }
    }
}

/// A night in one row: the hypnogram as a strip, and how long and how well.
///
/// Shared by Today, Body and the day's screen, so a night reads the same wherever it is opened from.
struct NightSummaryRow: View {
    let night: OuraEvent<OuraSleepPeriod>
    /// Which night this is, said when it is not the one the screen is about.
    var caption: String?

    @ScaledMetric(relativeTo: .body) private var stripHeight: CGFloat = 26

    var body: some View {
        let reading = night.reading
        VStack(alignment: .leading, spacing: 6) {
            if let caption {
                Text(caption)
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
            }
            // The facts beside the figure where they fit, under it where they do not, so neither
            // wraps a word at a time at the accessibility sizes.
            ViewThatFits(in: .horizontal) {
                HStack(alignment: .firstTextBaseline) {
                    asleep
                    Spacer()
                    factsText
                }
                VStack(alignment: .leading, spacing: 2) {
                    asleep
                    factsText
                }
            }
            if case .interval(let start, let end) = night.period, end > start {
                let runs = Hypnogram.runs(of: reading, from: start)
                if !runs.isEmpty {
                    LevelTimeline(
                        lanes: SleepStage.allCases,
                        laneName: \.name,
                        spans: runs.map { .init(lane: $0.stage, start: $0.start, end: $0.end, style: $0.stage.colour) },
                        domain: start...end,
                        isCompact: true)
                        .frame(height: stripHeight)
                        .clipShape(.rect(cornerRadius: 3))
                }
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(caption ?? "Night")
        .accessibilityValue("\(Formatting.hoursAndMinutes(seconds: Double(reading.totalSleepSeconds))) asleep, \(facts)")
    }

    private var asleep: some View {
        HStack(alignment: .firstTextBaseline, spacing: 6) {
            Text(Formatting.hoursAndMinutes(seconds: Double(night.reading.totalSleepSeconds)))
                .font(.headline.monospacedDigit())
            Text("asleep")
                .font(.subheadline)
                .foregroundStyle(Color(.secondaryLabel))
        }
        .fixedSize()
    }

    private var factsText: some View {
        Text(facts)
            .font(.subheadline.monospacedDigit())
            .foregroundStyle(Color(.secondaryLabel))
            .fixedSize(horizontal: false, vertical: true)
    }

    private var facts: String {
        let reading = night.reading
        var parts = ["\(reading.efficiency)% efficient"]
        if case .interval(let start, let end) = night.period {
            parts.append("\(start.formatted(date: .omitted, time: .shortened))–\(end.formatted(date: .omitted, time: .shortened))")
        }
        return parts.joined(separator: " · ")
    }
}
