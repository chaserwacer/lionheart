import SwiftUI

/// The span's cardio as the athlete divides it — zone 2, and the harder work in zones 3 to 5 — a
/// mean week of each, with zone 2 and the quality minutes the score credits against its targets.
///
/// The targets come from the score on screen, so this and the score cannot disagree; before the
/// score has loaded the figures stand alone.
struct ZoneSplitSection: View {
    let report: CardioReport
    let span: TrendSpan

    @Environment(TrainingBalanceStore.self) private var balance

    var body: some View {
        let split = ZoneSplit.of(report.weeks, sessions: report.sessions)
        let targets = balance.score.value?.cardio.zones ?? []
        Section {
            row(.zone2, minutes: split.zone2PerWeek, targets: targets)
            row(.quality, minutes: split.qualityPerWeek, targets: targets)
            LabeledContent("Zones 3–5") {
                Text("\(Formatting.measure(split.zones3To5PerWeek.rounded())) min").monospacedDigit()
            }
            LabeledContent("Sessions a week") {
                Text(split.sessionsPerWeek.formatted(.number.precision(.fractionLength(1)))).monospacedDigit()
            }
            if let share = split.hardShare {
                LabeledContent("Zones 3–5, of time at zone 2 and above") {
                    Text(share.formatted(.percent.precision(.fractionLength(0)))).monospacedDigit()
                }
            }
        } header: {
            Text("Zone 2 and zones 3–5, a mean week")
        } footer: {
            Text("Over the last \(span.days) days. Zone 2 is 60–70 % of maximum heart rate, zones 3–5 70 % and above. Quality is work bouts of two minutes or more at 80 % and above in runs, rides and other endurance sessions holding ten such minutes. The ticks are this week's targets in the training score.")
        }
        .task { await balance.refreshIfNeeded() }
    }

    @ViewBuilder
    private func row(_ band: ZoneBand, minutes: Double, targets: [Scored<ZoneTarget>]) -> some View {
        let target = targets.first { $0.of.band == band }?.of.target
        VStack(alignment: .leading, spacing: 4) {
            HStack(alignment: .firstTextBaseline) {
                Text(band.name)
                Spacer()
                Text(target.map { "\(Formatting.measure(minutes.rounded())) of \(Formatting.measure($0.target)) min" }
                     ?? "\(Formatting.measure(minutes.rounded())) min")
                    .monospacedDigit()
                    .foregroundStyle(Color(.secondaryLabel))
            }
            if let target {
                TargetBar(value: minutes, target: target.target)
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }
}
