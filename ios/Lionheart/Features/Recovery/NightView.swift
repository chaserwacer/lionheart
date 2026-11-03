import Charts
import SwiftUI

/// One night: how long and how well, the stages across it, and what the heart did.
///
/// The hypnogram and both overnight curves share one time axis, so a dip in HRV sits directly under
/// the stage it happened in. Every figure is Oura's: stage durations are its own totals, and the
/// hypnogram is the shape of the night those totals summarise.
struct NightView: View {
    let night: OuraEvent<OuraSleepPeriod>

    @ScaledMetric(relativeTo: .body) private var hypnogramHeight: CGFloat = 132
    @ScaledMetric(relativeTo: .body) private var curveHeight: CGFloat = 120

    private var reading: OuraSleepPeriod { night.reading }

    /// Bedtime to final waking, which every chart here is drawn across.
    private var span: ClosedRange<Date>? {
        guard case .interval(let start, let end) = night.period, end > start else { return nil }
        return start...end
    }

    var body: some View {
        List {
            Section {
                FigureGrid(figures: headline)
            } footer: {
                if let span {
                    Text("\(span.lowerBound.formatted(date: .omitted, time: .shortened)) to \(span.upperBound.formatted(date: .omitted, time: .shortened)), filed under \(Formatting.date(night.day)).")
                }
            }

            if let span {
                hypnogramSection(span)
            }
            stagesSection
            if let span {
                curveSection(
                    title: "Heart rate", unit: "bpm", series: reading.heartRate, span: span,
                    colour: Tokens.accent, marksLowest: true, summary: heartRateSummary)
                curveSection(
                    title: "HRV", unit: "ms", series: reading.hrv, span: span,
                    colour: .indigo, marksLowest: false, summary: reading.averageHrv.map { "Average \($0) ms" })
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Night of \(Formatting.date(night.day.adding(days: -1)))")
        .navigationBarTitleDisplayMode(.inline)
    }

    // MARK: - Headline

    private var headline: [OuraPanel.Figure] {
        [
            OuraPanel.Figure(label: "Asleep", value: Formatting.hoursAndMinutes(seconds: Double(reading.totalSleepSeconds))),
            OuraPanel.Figure(label: "In bed", value: Formatting.hoursAndMinutes(seconds: Double(reading.timeInBedSeconds))),
            OuraPanel.Figure(label: "Efficiency", value: "\(reading.efficiency)%"),
            OuraPanel.Figure(label: "Latency", value: MeasurementText.spelledDuration(seconds: Double(reading.latencySeconds))),
            reading.lowestHeartRate.flatMap { $0 > 0 ? OuraPanel.Figure(label: "Lowest HR", value: "\($0)") : nil },
            reading.averageHrv.map { OuraPanel.Figure(label: "Avg HRV", value: "\($0)") },
            reading.averageBreath.map { OuraPanel.Figure(label: "Breaths /min", value: MeasurementText.plain($0)) },
            OuraPanel.Figure(label: "Restless", value: "\(reading.restlessPeriods)"),
        ].compactMap(\.self)
    }

    // MARK: - Hypnogram

    @ViewBuilder
    private func hypnogramSection(_ span: ClosedRange<Date>) -> some View {
        let runs = Hypnogram.runs(of: reading, from: span.lowerBound)
        if !runs.isEmpty {
            Section {
                LevelTimeline(
                    lanes: SleepStage.allCases,
                    laneName: \.name,
                    spans: runs.map { .init(lane: $0.stage, start: $0.start, end: $0.end, style: $0.stage.colour) },
                    domain: span)
                    .frame(height: hypnogramHeight)
                    .padding(.vertical, 6)
                    .accessibilityLabel("Hypnogram")
                    .accessibilityValue(stageSummary)
            } header: {
                Text("Stages")
            } footer: {
                Text("Five-minute stages from Oura's hypnogram, decoded as Oura documents it: 1 deep, 2 light, 3 REM, 4 awake.")
            }
        }
    }

    // MARK: - Stage breakdown

    @ViewBuilder
    private var stagesSection: some View {
        let shares = StageShare.of(reading)
        if !shares.isEmpty {
            Section {
                Chart(shares) { share in
                    BarMark(x: .value("Share", share.fraction), stacking: .normalized)
                        .foregroundStyle(share.stage.colour)
                }
                .chartXAxis(.hidden)
                .chartYAxis(.hidden)
                .frame(height: 14)
                .clipShape(.capsule)
                .accessibilityHidden(true)

                Grid(alignment: .leading, horizontalSpacing: 12, verticalSpacing: 6) {
                    ForEach(shares) { share in
                        GridRow {
                            Circle().fill(share.stage.colour).frame(width: 8, height: 8)
                            Text(share.stage.name).font(.subheadline)
                            Text(Formatting.hoursAndMinutes(seconds: Double(share.seconds)))
                                .font(.subheadline.monospacedDigit())
                                .gridColumnAlignment(.trailing)
                            Text(share.fraction.formatted(.percent.precision(.fractionLength(0))))
                                .font(.subheadline.monospacedDigit())
                                .foregroundStyle(Color(.secondaryLabel))
                                .gridColumnAlignment(.trailing)
                        }
                        .accessibilityElement(children: .combine)
                    }
                }
                .padding(.vertical, 2)
            } header: {
                Text("Time in each stage")
            } footer: {
                Text("Oura's own stage totals, as shares of the time in bed.")
            }
        }
    }

    private var stageSummary: String {
        StageShare.of(reading)
            .map { "\($0.stage.name) \(Formatting.hoursAndMinutes(seconds: Double($0.seconds)))" }
            .joined(separator: ", ")
    }

    // MARK: - Overnight curves

    private var heartRateSummary: String? {
        var parts: [String] = []
        if let lowest = reading.lowestHeartRate, lowest > 0 { parts.append("Lowest \(lowest) bpm") }
        if let average = reading.averageHeartRate, average > 0 { parts.append("average \(MeasurementText.plain(average))") }
        return parts.isEmpty ? nil : parts.joined(separator: ", ")
    }

    @ViewBuilder
    private func curveSection(
        title: String, unit: String, series: OuraSampleSeries, span: ClosedRange<Date>,
        colour: Color, marksLowest: Bool, summary: String?
    ) -> some View {
        let runs = series.runs
        let points = runs.flatMap(\.self)
        if points.count >= 2 {
            Section {
                Chart {
                    ForEach(Array(runs.enumerated()), id: \.offset) { run, samples in
                        ForEach(Array(samples.enumerated()), id: \.offset) { _, point in
                            LineMark(
                                x: .value("Time", point.at),
                                y: .value(title, point.value),
                                series: .value("Stretch", run))
                                .interpolationMethod(.monotone)
                                .lineStyle(StrokeStyle(lineWidth: 1.5))
                                .foregroundStyle(colour)
                        }
                    }
                    if marksLowest, let lowest = points.min(by: { $0.value < $1.value }) {
                        PointMark(x: .value("Time", lowest.at), y: .value(title, lowest.value))
                            .symbolSize(24)
                            .foregroundStyle(colour)
                    }
                }
                .chartXScale(domain: span)
                .chartYScale(domain: .automatic(includesZero: false))
                .chartXAxis {
                    AxisMarks(values: .stride(by: .hour)) { _ in
                        AxisGridLine().foregroundStyle(Color(.separator))
                        AxisValueLabel(format: .dateTime.hour(.defaultDigits(amPM: .omitted)))
                    }
                }
                .frame(height: curveHeight)
                .padding(.vertical, 6)
                .accessibilityLabel("\(title) across the night")
                .accessibilityValue(summary ?? "")
            } header: {
                Text("\(title), \(unit)")
            } footer: {
                if let summary { Text("\(summary). Gaps are minutes the ring took no reading.") }
            }
        }
    }
}

/// Figures as a grid of tiles, three or four across, wrapping as Dynamic Type widens them.
///
/// A night or a day carries dozens of figures; as tiles the number leads and a panel reads at a
/// glance. The minimum width scales with the text, so the column count drops at the accessibility
/// sizes rather than any figure truncating.
struct FigureGrid: View {
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
