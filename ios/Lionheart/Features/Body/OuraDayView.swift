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
                    Label("Nothing on this day", systemImage: "moon.zzz")
                } description: {
                    Text("The ring filed nothing for it.")
                }
                .listRowBackground(Color.clear)
            } else {
                ForEach(panels) { panel in
                    Section(panel.title) {
                        if let score = panel.score {
                            LabeledContent("Score") { Text("\(score)").monospacedDigit() }
                        }
                        ForEach(panel.figures) { figure in
                            LabeledContent(figure.label) {
                                Text(figure.value).monospacedDigit()
                            }
                        }
                    }
                }
            }
        }
        .listStyle(.insetGrouped)
        .navigationTitle(Formatting.date(day.date))
        .navigationBarTitleDisplayMode(.inline)
    }

    // MARK: - The night

    @ViewBuilder
    private func nightSection(_ night: OuraSleepPeriod) -> some View {
        Section {
            HStack(alignment: .top, spacing: Tokens.cardPadding) {
                StatTile(
                    value: MeasurementText.clock(seconds: Double(night.totalSleepSeconds)),
                    label: "Asleep")
                StatTile(value: "\(night.efficiency)%", label: "Efficiency")
                if let hrv = night.averageHrv {
                    StatTile(value: "\(hrv)", label: "Avg HRV")
                }
            }

            ForEach(Self.stages(of: night)) { stage in
                LabeledContent(stage.label) {
                    Text(MeasurementText.clock(seconds: Double(stage.seconds)))
                        .monospacedDigit()
                }
            }

            // The rate Oura reports as zero on a period it took no reading across is shown as
            // absent rather than as a heart rate of zero. The stored zero is still the honest
            // record; it just is not a number to put on a screen.
            if let lowest = night.lowestHeartRate {
                LabeledContent("Lowest heart rate") { Text("\(lowest)").monospacedDigit() }
            }
            if let breath = night.averageBreath {
                LabeledContent("Breathing rate") {
                    Text("\(MeasurementText.plain(breath)) /min").monospacedDigit()
                }
            }
            LabeledContent("Restless periods") {
                Text("\(night.restlessPeriods)").monospacedDigit()
            }
        } header: {
            Text("The night")
        } footer: {
            Text(Self.nightFooter(night))
        }
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
                    Label(phrase, systemImage: "tag")
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
