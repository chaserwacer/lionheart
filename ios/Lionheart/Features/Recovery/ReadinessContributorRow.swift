import SwiftUI

/// One readiness contributor: Oura's score for it on a 0–100 track, and under it the measurement it
/// rests on.
///
/// The two are drawn in two different ways on purpose, so a score is never read as a measurement:
/// Oura's score is a mark on a track with the band he usually scores in, and the figure is a number
/// with its unit and its deviation from his 28 days before. The score's column is headed "Oura" in
/// the section, and the row says it again to VoiceOver.
struct ReadinessContributorRow: View {
    let reading: ReadinessContributorReading

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    @ScaledMetric(relativeTo: .subheadline) private var gaugeWidth: CGFloat = 84
    @ScaledMetric(relativeTo: .subheadline) private var scoreWidth: CGFloat = 26

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            if dynamicTypeSize.isAccessibilitySize {
                name
                if let oura = reading.oura { HStack(spacing: 8) { gauge(oura); score(oura) } }
            } else {
                HStack(spacing: 8) {
                    name.frame(maxWidth: .infinity, alignment: .leading)
                    if let oura = reading.oura {
                        gauge(oura).frame(width: gaugeWidth)
                        score(oura).frame(width: scoreWidth, alignment: .trailing)
                    }
                }
            }
            measurement
        }
        .padding(.vertical, 1)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(reading.contributor.name)
        .accessibilityValue(spoken)
    }

    private var name: some View {
        Text(reading.contributor.name)
            .font(.subheadline)
            .lineLimit(1)
    }

    private func gauge(_ oura: OuraContributorReading) -> some View {
        RangeGauge(
            value: oura.score.value,
            usual: oura.usualRange,
            mean: oura.baseline?.mean,
            mark: oura.standing == .unfavourable ? .orange : Color(.secondaryLabel))
            .frame(height: 12)
    }

    private func score(_ oura: OuraContributorReading) -> some View {
        Text(oura.score.description)
            .font(.caption.monospacedDigit())
            .foregroundStyle(oura.standing == .unfavourable ? .orange : Color(.secondaryLabel))
    }

    @ViewBuilder
    private var measurement: some View {
        if let basis = reading.basis {
            MeasurementLine(basis: basis)
        } else {
            Text(reading.restsOnTheNight ? "The night's measurements have not synced yet" : "No measurement behind this score")
                .font(.caption)
                .foregroundStyle(Color(.tertiaryLabel))
        }
    }

    private var spoken: String {
        let oura = reading.oura.map { "Oura's score \($0.score.description) of 100" } ?? "No Oura score"
        let measured = reading.basis.map { ", measured: \(MeasurementLine.text(of: $0))" } ?? ""
        return oura + measured
    }
}

/// What Lionheart measured behind a contributor, as one line of figures.
private struct MeasurementLine: View {
    let basis: ReadinessContributorReading.Basis

    var body: some View {
        switch basis {
        case .measured(let figure):
            HStack(alignment: .firstTextBaseline, spacing: 6) {
                figureText(figure)
                if let deviation = figure.deviation {
                    Text("\(Formatting.deviation(deviation, of: figure.metric)) vs usual")
                        .font(.footnote.monospacedDigit())
                        .foregroundStyle(figure.standing == .unfavourable ? .orange : Color(.secondaryLabel))
                }
            }
        case .lowestHeartRate, .recentSleep:
            Text(Self.text(of: basis))
                .font(.footnote.monospacedDigit())
                .foregroundStyle(Color(.label))
        }
    }

    private func figureText(_ figure: MetricReading) -> some View {
        let parts = Formatting.figure(figure.value, of: figure.metric)
        return Text("\(Formatting.metricName(figure.metric)) \(Text(parts.number).fontWeight(.semibold))\(parts.unit.map { " \($0)" } ?? "")")
            .font(.footnote.monospacedDigit())
    }

    /// The line as plain text, for VoiceOver and for the two bases that are one sentence anyway.
    static func text(of basis: ReadinessContributorReading.Basis) -> String {
        switch basis {
        case .measured(let figure):
            let value = Formatting.figureText(figure.value, of: figure.metric)
            let deviation = figure.deviation.map { ", \(Formatting.deviation($0, of: figure.metric)) against your usual" } ?? ""
            return "\(Formatting.metricName(figure.metric)) \(value)\(deviation)"
        case .lowestHeartRate(let timing):
            let percent = Int((timing.shareOfNight * 100).rounded())
            return "Lowest \(Int(timing.bpm.rounded())) bpm at \(timing.at.formatted(date: .omitted, time: .shortened)), \(percent)% into the night"
        case .recentSleep(let sleep):
            return "Last \(sleep.nights) nights: \(Formatting.hoursAndMinutes(seconds: sleep.meanAsleep)) asleep, \(Formatting.hoursAndMinutes(seconds: sleep.meanInBed)) in bed"
        }
    }
}
