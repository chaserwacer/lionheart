import SwiftUI

/// How the score, or one of its parts, is worked out: what is counted, against what, and what it
/// pays. Opened from the info button beside the figure it explains.
///
/// Targets and weights are read from the score on screen, so the sheet states the numbers the
/// server scored with. The shares a priority lift's standing pays are the server's `ScoreRules`,
/// restated in words.
struct ScoreInfoSheet: View {
    /// The part explained, or the whole score when `nil`.
    let part: ScorePart?
    let score: TrainingScore
    /// For the zone floors in beats per minute, when the window's cardio has been read.
    var maxHeartRate: MaxHeartRate?

    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            List {
                switch part {
                case nil: whole
                case .lifting?: lifting
                case .cardio?: cardio
                case .progress?: progress
                case .consistency?: consistency
                case .unrecognised?: Text("Not readable by this version.")
                }
            }
            .listStyle(.insetGrouped)
            .navigationTitle(part.map { "\($0.name) points" } ?? "Training score")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Done") { dismiss() }
                }
            }
        }
        .presentationDetents([.medium, .large])
    }

    // MARK: - The whole score

    @ViewBuilder
    private var whole: some View {
        Section {
            Text("Lionheart's own score for the seven days ending today — not Oura's activity score. It is earned by training, not by moving: 90 takes a committed week in every part at once, a new best included.")
        }
        Section("Four parts, by weight") {
            weight("Lifting", score.lifting.outOf, "Hard sets of push, pull and legs.")
            weight("Cardio", score.cardio.outOf, "Zone 2 minutes, and quality minutes: work bouts at zone 4–5 in endurance sessions.")
            weight("Progress", score.progress.outOf, "Each family's priority lift against its last 12 weeks.")
            weight("Consistency", score.consistency.outOf, "Sessions in each of the last four weeks.")
        }
        Section("Targets that follow you") {
            Text("Hard sets, zone 2 and quality minutes each have this week's target: the mean of the four weeks before plus 10 %, never under a floor and never over the committed standard — 8 sets a family, 150 zone 2 minutes, 40 quality minutes. Ten per cent keeps the week's load near 1.1 times the month's, the ramp tolerance builds on.")
        }
        Section("What a measurement pays") {
            Text("Half its points against this week's target and half against the committed standard, each the square root of how much was reached, capped at the whole. Meeting a comeback target of 4 sets pays 85 % of the family; only the committed 8 pays it all. The first sets and the first hour of zone 2 buy most of what more would, and the curve says so.")
        }
        Section("Unknown, not zero") {
            Text("A part with no data — cardio with the ring off all week — is unknown. Its points are out of reach and the score reads out of what could be read, so a week with the ring off never outscores the same week worn.")
        }
        Section("The chart") {
            Text("Three rows on one axis of 100 points: the score, its four parts, and every measurement under its part. Each cell is as wide as the points it can earn and filled with the points it did, so an empty stretch in the bottom row reads straight up to the part and the points it cost. The dashed line is 90. Tap a part to open it.")
        }
    }

    // MARK: - Parts

    @ViewBuilder
    private var lifting: some View {
        Section {
            Text("Hard sets in the seven days — working, drop and failure sets, never warm-ups — of push, pull and legs, each against this week's target: the family's mean of the four weeks before plus 10 %, between 4 and the committed 8. A set counts toward the family its movement trains, as each movement's page states.")
        }
        Section("What it pays") {
            Text("Each family is a third of \(score.lifting.outOf) points, half paid against this week's target and half against the committed 8. Over the target in one family pays nothing toward another: 24 sets of bench press and no legs earns a third.")
        }
    }

    @ViewBuilder
    private var cardio: some View {
        let zone2 = score.cardio.zones.first { $0.of.band == .zone2 }
        let quality = score.cardio.zones.first { $0.of.band == .quality }
        Section("Zones") {
            Text(zones)
        }
        Section("What it pays") {
            Text("Zone 2 against this week's \(target(zone2?.of.target)) minutes pays \(points(zone2?.outOf)); quality against this week's \(target(quality?.of.target)) minutes pays \(points(quality?.outOf)). Zone 2 is the aerobic base; quality is the hard work that adapts the top end.")
        }
        Section("Quality, not just high heart rate") {
            Text("Heat, caffeine, stress and a crowd raise heart rate without training anything, so a minute at a high heart rate is not credited on its own. A quality minute is inside a work bout — heart rate held at 80 % of maximum or above for two minutes or more, a dip of half a minute allowed — in a run, ride, row, swim or machine session whose bouts add up to at least 10 minutes: 5 × 2 min, 4 × 4 min, a 20-minute threshold block.")
            Text("Walks, hikes and games earn zone 2 but never quality: the terrain or the play sets their effort. Zone 3 counts in neither band.")
                .foregroundStyle(Color(.secondaryLabel))
        }
        Section("What counts") {
            Text("Minutes inside cardio sessions only: logged runs, runs imported from Apple Health, and workouts entered on the ring. Lifting never counts, and neither do walks under 30 minutes.")
            Text("A game or dance the ring detected on its own never counts, whatever its heart rate: a night out reads like a session. Any other workout it detected counts only when its heart rate shows training: at least half of it read, at least 20 minutes at zone 2 or above or 10 at zone 3 or above, and zone 2 or above for at least half the time read. Housework, yard work and stretching never count.")
            Text("Everything left out is listed with its heart rate under the week's sessions.")
                .foregroundStyle(Color(.secondaryLabel))
        }
        Section("Unknown") {
            Text("Unknown rather than zero when no heart rate was taken all week — by the ring or by a watch recording a session — when the profile has no age to draw zones from, or when most of the week's session time has no heart rate behind it.")
        }
    }

    @ViewBuilder
    private var progress: some View {
        Section {
            Text("Each family's priority lift — the one pinned, or else the most-trained compound lift of 8 weeks — read as its best estimated single in the seven days against its best of the 12 weeks before.")
        }
        Section("What it pays") {
            Text("Each family is a third of \(score.progress.outOf) points. A new 12-week or lifetime best pays its third in full; held within 5 %, or first back after 12 weeks, a quarter; below, or trained with no estimate, a tenth; not trained, nothing.")
            Text("With every other part full, holding all three lifts reads 85: 90 takes a new best.")
                .foregroundStyle(Color(.secondaryLabel))
        }
    }

    @ViewBuilder
    private var consistency: some View {
        Section {
            Text("Completed sessions — lifting and logged cardio — in each of the last four seven-day blocks, against \(score.consistency.target) a block. A session of nothing but walking is not one.")
        }
        Section("What it pays") {
            Text("Each block is a quarter of \(score.consistency.outOf) points. Four weeks because one cannot show consistency: a single big week after three empty ones still reads low.")
        }
    }

    // MARK: - Words

    private var zones: String {
        let scheme = "Zones are percentages of maximum heart rate: zone 2 is 60–70 %, zone 4 begins at 80 %."
        guard let bpm = maxHeartRate?.bpm else { return scheme }
        let floor = { (fraction: Double) in Int((fraction * Double(bpm)).rounded(.up)) }
        return "\(scheme) With a maximum of \(bpm) bpm, zone 2 runs from \(floor(0.6)) to \(floor(0.7) - 1) bpm and a work bout holds \(floor(0.8)) or above. The cardio screen says where the maximum comes from."
    }

    private func weight(_ name: String, _ outOf: Int, _ text: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(alignment: .firstTextBaseline) {
                Text(name)
                Spacer()
                Text("\(outOf) points").monospacedDigit().foregroundStyle(Color(.secondaryLabel))
            }
            Text(text).font(.footnote).foregroundStyle(Color(.secondaryLabel))
        }
        .accessibilityElement(children: .combine)
    }

    private func target(_ value: WeeklyTarget?) -> String { value.map { Formatting.measure($0.target) } ?? "its target" }

    private func points(_ value: Double?) -> String { value.map { "\(ScorePoints.text($0)) points" } ?? "its points" }
}
