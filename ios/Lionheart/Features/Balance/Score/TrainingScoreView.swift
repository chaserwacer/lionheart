import SwiftUI

/// The training score in full: the number, half a year of weeks, and each part as the measurements
/// it was read from against their targets.
///
/// Lionheart's score, not Oura's: it is earned by doing the training rather than by moving, so a
/// decent week reads in the 60s and 90 takes every part at once. Each part says what it counted and
/// why, and a part with no data says it is unknown rather than showing zero.
struct TrainingScoreView: View {
    @Environment(TrainingBalanceStore.self) private var balance

    var body: some View {
        List {
            switch balance.score {
            case .loading:
                Section { SkeletonRows(count: 4) }
            case .unavailable(let failure):
                ContentUnavailableView {
                    Text("Score not available")
                } description: {
                    Text(failure.message)
                } actions: {
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await balance.refresh() } }
                    }
                }
                .listRowBackground(Color.clear)
            case .loaded(let score):
                content(score)
            case .stale(let score, let staleness):
                StaleBanner(staleness)
                content(score)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Training score")
        .navigationBarTitleDisplayMode(.inline)
        .refreshable { await balance.refresh() }
        .task { await balance.refreshIfNeeded() }
    }

    @ViewBuilder
    private func content(_ score: TrainingScore) -> some View {
        headline(score)
        trend(score)
        liftingSection(score.lifting)
        cardioSection(score.cardio)
        progressSection(score.progress)
        consistencySection(score.consistency)
        Section {
            Text(Self.method)
                .font(.footnote)
                .foregroundStyle(Color(.secondaryLabel))
        } header: {
            Text("How it is scored")
        }
    }

    private func headline(_ score: TrainingScore) -> some View {
        Section {
            VStack(alignment: .leading, spacing: 4) {
                HStack(alignment: .firstTextBaseline, spacing: 6) {
                    Text("\(score.value)")
                        .font(.system(.largeTitle, design: .default).weight(.bold))
                        .monospacedDigit()
                    Text("of 100")
                        .foregroundStyle(Color(.secondaryLabel))
                }
                Text("7 days, \(Formatting.date(score.window.from)) – \(Formatting.date(score.window.to))")
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
                if score.isPartial {
                    Text(partialNote(score))
                        .font(.subheadline)
                        .foregroundStyle(.orange)
                }
            }
            .padding(.vertical, 4)
        } footer: {
            Text("Seven days ending today rather than a calendar week, so the score never empties on a Monday.")
        }
    }

    @ViewBuilder
    private func trend(_ score: TrainingScore) -> some View {
        if let weeks = balance.weeks.value, !weeks.isEmpty {
            Section {
                WeeklyScoreChart(weeks: weeks, highlighted: weeks.last?.window.to)
                    .frame(height: 140)
                    .padding(.vertical, 6)
            } header: {
                Text("Weeks")
            } footer: {
                Text(trendNote(weeks))
            }
        }
    }

    private func liftingSection(_ part: LiftingPart) -> some View {
        Section {
            ForEach(ScoreBreakdown.lifting(part)) { ScoreMeasureRow(measure: $0) }
        } header: {
            PartHeader(name: "Lifting", points: part.points, outOf: part.outOf)
        } footer: {
            Text("Hard sets of push, pull and legs against 8 each. Over 8 in one family does not make up for another.")
        }
    }

    @ViewBuilder
    private func cardioSection(_ part: CardioPart) -> some View {
        Section {
            switch part.measure {
            case .measured(_, _, _, _, let sessions, let heartRateDays):
                ForEach(ScoreBreakdown.cardio(part.measure)) { ScoreMeasureRow(measure: $0) }
                LabeledContent("Cardio sessions") {
                    Text("\(sessions)").monospacedDigit()
                }
                if heartRateDays < 7 {
                    Text("Heart rate on \(heartRateDays) of 7 days; minutes on the others are not counted.")
                        .font(.footnote)
                        .foregroundStyle(Color(.secondaryLabel))
                }
            case .unknown(let reason):
                Text(Self.unknown(reason))
                    .foregroundStyle(Color(.secondaryLabel))
            case .unrecognised:
                Text("Not readable by this version.")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        } header: {
            PartHeader(name: "Cardio", points: part.points, outOf: part.outOf)
        } footer: {
            Text("Zone 2 is 60–70 % of maximum heart rate, against 150 minutes a week; zones 4–5 are 80 % and above, against 20. Counted inside cardio sessions only — logged runs and the ring's workouts, not lifting or walks under 30 minutes.")
        }
    }

    private func progressSection(_ part: ProgressPart) -> some View {
        Section {
            ForEach(part.families) { family in
                NavigationLink(value: HistoryRoute.focus) {
                    PriorityProgressRow(family: family.family, lift: family.lift)
                }
            }
        } header: {
            PartHeader(name: "Progress", points: part.points, outOf: part.outOf)
        } footer: {
            Text("Each family's priority lift: its best estimated single this week against its best of the 12 weeks before. A new best pays in full; holding within 5 % or coming back after 12 weeks pays half.")
        }
    }

    private func consistencySection(_ part: ConsistencyPart) -> some View {
        Section {
            ForEach(ScoreBreakdown.consistency(part)) { ScoreMeasureRow(measure: $0) }
        } header: {
            PartHeader(name: "Consistency", points: part.points, outOf: part.outOf)
        } footer: {
            Text("Sessions in each of the last four 7-day blocks against \(part.target), your median in every block you have trained.")
        }
    }

    // MARK: - Words

    private func partialNote(_ score: TrainingScore) -> String {
        let names = score.unknown.map { $0.name.lowercased() }
        let parts = names.count > 1 ? names.dropLast().joined(separator: ", ") + " and " + names[names.count - 1] : names.first ?? ""

        return "\(parts.capitalisingFirst) unknown, so the score is read over the parts that are known, and cannot reach 90."
    }

    private func trendNote(_ weeks: [WeeklyScore]) -> String {
        let excellent = weeks.filter { $0.value >= WeeklyScoreChart.excellent }.count
        let partial = weeks.contains(where: \.isPartial) ? " Pale bars were read with a part unknown." : ""
        let reached = excellent == 0 ? "none has reached it" : "\(Formatting.count(excellent, "week")) reached it"
        return "Each week to its Sunday, the last in colour. The dashed line is 90; \(reached).\(partial)"
    }

    private static func unknown(_ reason: CardioUnknownReason) -> String {
        switch reason {
        case .noHeartRate: "Unknown: no heart rate from the ring this week."
        case .noMaximumHeartRate: "Unknown: the profile has no age to draw heart-rate zones from."
        case .sessionsUnrecorded: "Unknown: most of the week's cardio session time has no heart rate behind it."
        case .unrecognised: "Unknown."
        }
    }

    private static let method = "Lifting 35, cardio 25 (zone 2 15, zones 4–5 10), progress 20, consistency 20. Each part is its measurements against their targets, capped at the target. A score of 90 or more needs every part met in the same week: push, pull and legs at 8 hard sets, 150 minutes of zone 2 and 20 at high intensity, new bests on the priority lifts, and three sessions a week for four weeks."
}

/// A part's name and the points it earned, as the section header.
private struct PartHeader: View {
    let name: String
    let points: Double?
    let outOf: Int

    var body: some View {
        HStack {
            Text(name)
            Spacer()
            Text(points.map { "\(Formatting.measure($0.rounded())) of \(outOf)" } ?? "unknown")
                .monospacedDigit()
        }
    }
}

/// One measurement against its target, with the bar between them.
struct ScoreMeasureRow: View {
    let measure: ScoreMeasure

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack(alignment: .firstTextBaseline) {
                Text(measure.label)
                Spacer()
                Text(figure)
                    .monospacedDigit()
                    .foregroundStyle(Color(.secondaryLabel))
            }
            TargetBar(value: measure.measured, target: measure.target)
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(measure.label)
        .accessibilityValue(figure)
    }

    private var figure: String {
        let unit = switch measure.unit {
        case .hardSets: "hard sets"
        case .minutes: "min"
        case .sessions: "sessions"
        }
        return "\(Formatting.measure(measure.measured.rounded())) of \(Formatting.measure(measure.target)) \(unit)"
    }
}
