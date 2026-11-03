import Charts
import SwiftUI

/// One recovery figure over a month, a quarter or a year.
///
/// Each day is a faint point and the seven-day mean is the line the eye follows: a single night's
/// HRV moves more than a week of training does, and drawing the raw points as the line would make
/// noise look like signal. The mean of the span is a dashed rule, and pressing on the chart reads
/// out one day.
struct MetricTrendView: View {
    let metric: RecoveryMetric
    let endingOn: CalendarDay
    /// The trend held for a key, if one has been asked for.
    let trend: (MetricTrendKey) -> LoadState<MetricTrend>?
    let load: (MetricTrendKey) async -> Void

    @State private var span: TrendSpan = .quarter
    @State private var selectedDate: Date?

    @ScaledMetric(relativeTo: .body) private var chartHeight: CGFloat = 220

    private var key: MetricTrendKey { MetricTrendKey(metric: metric, span: span, endingOn: endingOn) }

    var body: some View {
        List {
            Section {
                Picker("Span", selection: $span) {
                    ForEach(TrendSpan.allCases) { span in
                        Text(span.label).tag(span)
                    }
                }
                .pickerStyle(.segmented)
                .listRowBackground(Color.clear)
                .listRowInsets(EdgeInsets())
            }

            switch trend(key) ?? .loading {
            case .loading:
                Section { SkeletonRows(count: 3) }
            case .unavailable(let failure):
                ContentUnavailableView {
                    Text("Trend not available")
                } description: {
                    Text(failure.message)
                } actions: {
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await load(key) } }
                    }
                }
                .listRowBackground(Color.clear)
            case .loaded(let trend):
                content(trend, staleBecause: nil)
            case .stale(let trend, let staleness):
                content(trend, staleBecause: staleness)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle(Formatting.metricTitle(metric))
        .navigationBarTitleDisplayMode(.inline)
        .refreshable { await load(key) }
        .task(id: key) {
            if trend(key)?.value == nil { await load(key) }
        }
    }

    @ViewBuilder
    private func content(_ trend: MetricTrend, staleBecause staleness: Staleness?) -> some View {
        if let staleness { StaleBanner(staleness) }

        if trend.points.count < 2 {
            ContentUnavailableView {
                Text("Not enough data")
            } description: {
                Text("The ring filed \(Formatting.count(trend.points.count, "day")) of \(Formatting.metricTitle(metric).lowercased()) in this span.")
            }
            .listRowBackground(Color.clear)
        } else {
            Section {
                readout(trend)
                chart(trend)
            } footer: {
                Text("Points are days; the line is the seven-day mean; the dashed rule is the mean of the span.")
            }
            summary(trend)
        }
    }

    // MARK: - Readout

    /// The figure at the pressed day, or the span's mean when nothing is pressed.
    private func readout(_ trend: MetricTrend) -> some View {
        let selected = selectedDate.flatMap { date in
            trend.points.min { abs($0.day.middayDate().timeIntervalSince(date)) < abs($1.day.middayDate().timeIntervalSince(date)) }
        }
        let value = selected?.value ?? trend.mean ?? 0
        let parts = Formatting.figure(value, of: metric)
        let caption = selected.map { Formatting.date($0.day) } ?? "\(span.label) mean"

        return VStack(alignment: .leading, spacing: 2) {
            Text(caption)
                .font(.subheadline)
                .foregroundStyle(Color(.secondaryLabel))
            HStack(alignment: .firstTextBaseline, spacing: 4) {
                Text(parts.number).font(.title.weight(.semibold).monospacedDigit())
                if let unit = parts.unit {
                    Text(unit).font(.subheadline).foregroundStyle(Color(.secondaryLabel))
                }
            }
        }
        .accessibilityElement(children: .combine)
    }

    // MARK: - Chart

    private func chart(_ trend: MetricTrend) -> some View {
        Chart {
            if let mean = trend.mean {
                RuleMark(y: .value("Mean", mean))
                    .lineStyle(StrokeStyle(lineWidth: 1, dash: [3, 3]))
                    .foregroundStyle(Color(.tertiaryLabel))
            }
            ForEach(trend.points) { point in
                PointMark(x: .value("Day", point.day.middayDate()), y: .value("Value", point.value))
                    .symbolSize(span == .year ? 8 : 16)
                    .foregroundStyle(Color(.tertiaryLabel))
            }
            ForEach(Array(MetricTrend.unbrokenRuns(of: trend.rollingMean).enumerated()), id: \.offset) { run, points in
                ForEach(points) { point in
                    LineMark(
                        x: .value("Day", point.day.middayDate()),
                        y: .value("Seven-day mean", point.value),
                        series: .value("Stretch", run))
                        .interpolationMethod(.monotone)
                        .lineStyle(StrokeStyle(lineWidth: 2))
                        .foregroundStyle(Tokens.accent)
                }
            }
            if let selectedDate {
                RuleMark(x: .value("Selected", selectedDate))
                    .foregroundStyle(Color(.separator))
            }
        }
        .chartYScale(domain: .automatic(includesZero: false))
        .chartXSelection(value: $selectedDate)
        .chartXAxis {
            AxisMarks(values: .automatic(desiredCount: 4)) { value in
                AxisGridLine()
                AxisValueLabel {
                    if let date = value.as(Date.self) {
                        Text(date, format: span == .year ? .dateTime.month(.abbreviated) : .dateTime.month(.abbreviated).day())
                    }
                }
            }
        }
        .frame(height: chartHeight)
        .padding(.vertical, 8)
        .accessibilityLabel("\(Formatting.metricTitle(metric)) over \(span.label)")
        .accessibilityValue(accessibilitySummary(trend))
    }

    // MARK: - Summary

    @ViewBuilder
    private func summary(_ trend: MetricTrend) -> some View {
        Section {
            if let mean = trend.mean {
                LabeledContent("Mean") { figureText(mean) }
            }
            if let lowest = trend.lowest {
                LabeledContent("Lowest") { figureText(lowest.value, on: lowest.day) }
            }
            if let highest = trend.highest {
                LabeledContent("Highest") { figureText(highest.value, on: highest.day) }
            }
            LabeledContent("Days with data") {
                Text("\(trend.points.count) of \(span.days)").monospacedDigit()
            }
        }
    }

    private func figureText(_ value: Double, on day: CalendarDay? = nil) -> some View {
        let text = Formatting.figureText(value, of: metric)
        return Text(day.map { "\(text) · \(Formatting.date($0))" } ?? text)
            .monospacedDigit()
    }

    private func accessibilitySummary(_ trend: MetricTrend) -> String {
        guard let mean = trend.mean, let lowest = trend.lowest, let highest = trend.highest else { return "" }
        return "Mean \(Formatting.figureText(mean, of: metric)), from \(Formatting.figureText(lowest.value, of: metric)) to \(Formatting.figureText(highest.value, of: metric))."
    }
}
