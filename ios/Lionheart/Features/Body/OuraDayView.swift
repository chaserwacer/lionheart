import SwiftUI

/// One Oura day: the night, what the ring saw, and each collection it filed.
///
/// The night leads, because it is the richest thing here and the thing the daily sleep score
/// flattens into one number. The panels below it come from `OuraPanel.panels(of:)` rather than
/// being written out, so a day with sleep and no readiness simply has one section fewer — and a
/// collection Oura adds lands as a case and a section rather than a redesign.
struct OuraDayView: View {
    let day: OuraDay
    let night: OuraSleepPeriod?
    let workouts: [OuraWorkout]
    let tags: [OuraTag]

    var body: some View {
        List {
            if let night {
                nightSection(night)
            }
            if !tags.isEmpty {
                tagSection
            }
            if !workouts.isEmpty {
                workoutSection
            }

            let panels = OuraPanel.panels(of: day)
            if panels.isEmpty, night == nil {
                ContentUnavailableView {
                    Text("Nothing on this day")
                } description: {
                    Text("The ring filed nothing for it.")
                }
                .listRowBackground(Color.clear)
            } else {
                ForEach(panels) { panel in
                    Section(panel.title) {
                        let score = panel.score.map { OuraPanel.Figure(label: "Score", value: "\($0)") }
                        FigureGrid(figures: (score.map { [$0] } ?? []) + panel.figures)
                    }
                }
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle(Formatting.date(day.date))
        .navigationBarTitleDisplayMode(.inline)
    }

    // MARK: - The night

    @ViewBuilder
    private func nightSection(_ night: OuraSleepPeriod) -> some View {
        Section {
            FigureGrid(figures: Self.summary(of: night))
            FigureGrid(figures: Self.stages(of: night).map {
                OuraPanel.Figure(label: $0.label, value: MeasurementText.clock(seconds: Double($0.seconds)))
            })
            FigureGrid(figures: Self.vitals(of: night))
        } header: {
            Text("The night")
        } footer: {
            Text(Self.nightFooter(night))
        }
    }

    /// The night in three figures: how long, how well, and the HRV it averaged.
    private static func summary(of night: OuraSleepPeriod) -> [OuraPanel.Figure] {
        [
            OuraPanel.Figure(label: "Asleep", value: MeasurementText.clock(seconds: Double(night.totalSleepSeconds))),
            OuraPanel.Figure(label: "Efficiency", value: "\(night.efficiency)%"),
        ] + (night.averageHrv.map { [OuraPanel.Figure(label: "Avg HRV", value: "\($0)")] } ?? [])
    }

    /// What the body did across the night.
    ///
    /// The rate Oura reports as zero on a period it took no reading across is shown as absent
    /// rather than as a heart rate of zero. The stored zero is still the honest record; it just is
    /// not a number to put on a screen.
    private static func vitals(of night: OuraSleepPeriod) -> [OuraPanel.Figure] {
        [
            night.lowestHeartRate.map { OuraPanel.Figure(label: "Lowest HR", value: "\($0)") },
            night.averageBreath.map {
                OuraPanel.Figure(label: "Breathing", value: "\(MeasurementText.plain($0)) /min")
            },
            OuraPanel.Figure(label: "Restless periods", value: "\(night.restlessPeriods)"),
        ].compactMap(\.self)
    }

    /// The stage breakdown, and only the stages the night actually had.
    static func stages(of night: OuraSleepPeriod) -> [Stage] {
        [
            Stage(label: "Deep", seconds: night.deepSleepSeconds),
            Stage(label: "REM", seconds: night.remSleepSeconds),
            Stage(label: "Light", seconds: night.lightSleepSeconds),
            Stage(label: "Awake", seconds: night.awakeSeconds),
        ].filter { $0.seconds > 0 }
    }

    struct Stage: Identifiable {
        let label: String
        let seconds: Int

        var id: String { label }
    }

    private static func nightFooter(_ night: OuraSleepPeriod) -> String {
        var parts = ["In bed \(MeasurementText.clock(seconds: Double(night.timeInBedSeconds)))"]
        if night.latencySeconds > 0 {
            parts.append(
                "asleep after \(MeasurementText.spelledDuration(seconds: Double(night.latencySeconds)))")
        }
        return parts.joined(separator: ", ") + "."
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
            Text("What the athlete marked on the ring — the closest thing here to their own account of the day.")
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

/// Figures as a grid of tiles, three or four across, wrapping as Dynamic Type widens them.
///
/// A day carries dozens of figures; as tiles the number leads and a whole panel reads at a glance,
/// where a labelled row per figure would run to several screens. The minimum width scales with the
/// text, so the column count drops at the accessibility sizes rather than any figure truncating.
private struct FigureGrid: View {
    let figures: [OuraPanel.Figure]

    @ScaledMetric(relativeTo: .headline) private var minimumWidth: CGFloat = 92

    var body: some View {
        LazyVGrid(
            columns: [GridItem(.adaptive(minimum: minimumWidth), spacing: Tokens.cardPadding, alignment: .topLeading)],
            alignment: .leading,
            spacing: 10
        ) {
            ForEach(figures) { figure in
                StatTile(value: figure.value, label: figure.label)
            }
        }
        .padding(.vertical, 2)
    }
}
