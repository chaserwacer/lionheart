import SwiftUI

/// The going-in rating taken apart: each part's score, the share of the rating it carried, the
/// figures it was read from, and the rule that turned them into a score.
///
/// The rules are stated as the server applies them (`GoingInAnalysis`), so the athlete can redo the
/// arithmetic from what is on this screen.
struct GoingInSheet: View {
    let state: GoingInState

    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            List {
                ratingSection
                readinessSection
                autonomicSection
                sleepSection
                loadSection
                wellnessSection
            }
            .listStyle(.insetGrouped)
            .navigationTitle("Going in")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Done") { dismiss() }
                }
            }
        }
        .presentationDetents([.medium, .large])
        .presentationDragIndicator(.visible)
    }

    private var ratingSection: some View {
        Section {
            HStack(alignment: .firstTextBaseline) {
                Text(GoingInWords.score(state.rating))
                    .font(.largeTitle.monospacedDigit().weight(.bold))
                    .foregroundStyle(GoingInWords.tint(state.rating))
                Text(GoingInWords.band(state.rating))
                    .font(.title3)
                Spacer()
                Text("as of \(state.asOf.formatted(date: .omitted, time: .shortened))")
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
            }
            .accessibilityElement(children: .combine)
        } footer: {
            Text("""
                Lionheart's own rating, not Oura's. Each part is scored 0–100 with your usual day near \
                75, and the rating is their weighted mean: readiness 30 %, HRV and heart rate 25 %, \
                sleep 20 %, load 15 %, wellness 10 %. A part that cannot be read is left out and the \
                others carry its weight. Without readiness, last night or its sleep score there is no \
                rating. 80 and above is high, 65 normal, 50 reduced.
                """)
        }
    }

    private var readinessSection: some View {
        Section {
            figure("Oura readiness", state.readiness.ouraScore.map { "\($0.rounded)" })
            if let low = state.readiness.usualLow, let high = state.readiness.usualHigh {
                figure("Usual range, 90 days", "\(low.rounded)–\(high.rounded)")
            }
            figure("HRV balance (Oura)", state.readiness.hrvBalance.map { "\($0.rounded)" })
            figure("Resting heart rate (Oura)", state.readiness.restingHeartRate.map { "\($0.rounded)" })
            figure(
                "Temperature",
                state.readiness.temperatureDeviation.map { Formatting.figureText($0, of: .temperatureDeviation) })
        } header: {
            partHeader(.readiness, state.readiness.score)
        } footer: {
            Text("Oura's readiness score, unchanged. The contributors are Oura's own 0–100 ratings against your baseline, not measurements.")
        }
    }

    private var autonomicSection: some View {
        Section {
            figure("HRV last night", state.autonomic.hrv.map { Formatting.figureText(Double($0), of: .hrv) })
            figure("HRV baseline", state.autonomic.hrvBaseline.map { Formatting.figureText($0, of: .hrv) })
            figure(
                "Lowest heart rate",
                state.autonomic.lowestHeartRate.map { Formatting.figureText(Double($0), of: .lowestHeartRate) })
            figure(
                "Lowest heart rate baseline",
                state.autonomic.lowestHeartRateBaseline.map { Formatting.figureText($0, of: .lowestHeartRate) })
            figure("Baseline nights", "\(state.autonomic.baselineNights)")
        } header: {
            partHeader(.autonomic, state.autonomic.score)
        } footer: {
            Text("""
                Last night against the nights before the last two, at least 14 of them. HRV scores 75 at \
                baseline and moves 1.25 for each per cent above or below it; lowest heart rate moves 8 \
                the other way for each beat. The two halves are averaged.
                """)
        }
    }

    private var sleepSection: some View {
        Section {
            night("Last night", state.sleep.lastNight)
            night("Night before", state.sleep.nightBefore)
        } header: {
            partHeader(.sleep, state.sleep.score)
        } footer: {
            Text("Oura's sleep score for last night and the night before, two to one. Oura files a night under the day you woke, and some mornings files readiness before the night.")
        }
    }

    private var loadSection: some View {
        Section {
            figure("Hard sets since yesterday", "\(state.load.hardSets)")
            figure("Zone 4 and above", state.load.highIntensityMinutes.map(Formatting.minutes) ?? "Not read")
            figure("Zone 2", state.load.zoneTwoMinutes.map(Formatting.minutes) ?? "Not read")
            figure(
                "Hard sets, 7 days",
                "\(state.load.hardSetsLast7) against \(Formatting.measure((state.load.hardSetsWeeklyMean28 * 10).rounded() / 10)) a week")
        } header: {
            partHeader(.load, state.load.score)
        } footer: {
            Text("""
                Since the start of yesterday: 100, less 1.5 a hard set, 1 a minute at zone 4 or above and \
                0.15 a minute of zone 2, and 50 for each unit the last 7 days' hard sets run above 1.3 \
                times your four-week weekly mean.
                """)
        }
    }

    private var wellnessSection: some View {
        Section {
            if let today = state.wellness.today {
                figure("Overall", Formatting.measure(today.overall))
                figure("Mood", "\(today.mood)")
                figure("Energy", "\(today.energy)")
                figure("Stress", "\(today.stress)")
                figure("Motivation", "\(today.motivation)")
            } else {
                Text("Not rated today")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        } header: {
            partHeader(.wellness, state.wellness.score)
        } footer: {
            Text("Today's 1–5 ratings, stress inverted, their mean put on the same scale: 1 is 0, 5 is 100.")
        }
    }

    // MARK: - Pieces

    private func partHeader(_ kind: GoingInReading.Part.Kind, _ score: PartScore) -> some View {
        HStack {
            Text(GoingInWords.name(kind))
            Spacer()
            switch score {
            case .scored(let value, let share):
                Text("\(value) · \(Int((share * 100).rounded())) % of rating")
                    .monospacedDigit()
            case .notScored, .unrecognised:
                Text("Not scored")
            }
        }
    }

    @ViewBuilder
    private func figure(_ label: String, _ value: String?) -> some View {
        LabeledContent(label) {
            Text(value ?? "Not read")
                .monospacedDigit()
                .foregroundStyle(value == nil ? Color(.tertiaryLabel) : Color(.secondaryLabel))
        }
    }

    private func night(_ label: String, _ night: GoingInState.NightNote?) -> some View {
        LabeledContent {
            if let night {
                VStack(alignment: .trailing, spacing: 2) {
                    Text(Formatting.hoursAndMinutes(seconds: night.totalSleep))
                        .monospacedDigit()
                    Text([
                        "\(night.efficiency) % efficient",
                        night.sleepScore.map { "score \($0.rounded)" },
                    ].compactMap { $0 }.joined(separator: " · "))
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
                }
            } else {
                Text("Not synced")
                    .foregroundStyle(Color(.tertiaryLabel))
            }
        } label: {
            VStack(alignment: .leading, spacing: 2) {
                Text(label)
                if let night {
                    Text(Formatting.dayInline(night.day))
                        .font(.caption)
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
        }
    }
}
