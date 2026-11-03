import Charts
import SwiftUI

/// How the days are shaped: how long he sits at a time, when he moves and trains, how regular his
/// sleep is, weekdays against weekends — each against the span before.
///
/// Measured and comparative rather than advisory: every figure is his own, and the only judgement
/// on the screen is the orange on a change that went the worse way.
struct HabitsView: View {
    let store: HabitsStore

    @State private var span: TrendSpan = .month
    @State private var dayKind: DayKind = .weekdays

    @ScaledMetric(relativeTo: .body) private var chartHeight: CGFloat = 180
    @ScaledMetric(relativeTo: .body) private var smallChartHeight: CGFloat = 64
    private let barWidth: CGFloat = 9

    enum DayKind: Hashable, CaseIterable {
        case weekdays, weekends

        var label: String {
            switch self {
            case .weekdays: "Weekdays"
            case .weekends: "Weekends"
            }
        }
    }

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

            switch store.reports[span] ?? .loading {
            case .loading:
                Section { SkeletonRows(count: 5) }
            case .unavailable(let failure):
                ContentUnavailableView {
                    Text("Movement not available")
                } description: {
                    Text(failure.message)
                } actions: {
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await store.load(span) } }
                    }
                }
                .listRowBackground(Color.clear)
            case .loaded(let report):
                content(report, staleBecause: nil)
            case .stale(let report, let staleness):
                content(report, staleBecause: staleness)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Daily movement")
        .refreshable { await store.load(span) }
        .task(id: span) {
            if store.reports[span]?.value == nil { await store.load(span) }
        }
    }

    @ViewBuilder
    private func content(_ report: HabitsReport, staleBecause staleness: Staleness?) -> some View {
        if let staleness { StaleBanner(staleness) }

        if report.current.allDays.completeDays == 0 {
            ContentUnavailableView {
                Text("No complete days")
            } description: {
                Text("No day in this span has Oura's five-minute classification with the ring worn. Days synced before the classification was stored need the archive re-imported or a sync.")
            }
            .listRowBackground(Color.clear)
        } else {
            comparisonSection(report)
            hoursSection(report)
            sittingSection(report)
            sleepSection(report)
            stepsSection(report)
            daysSection(report)
        }
    }

    // MARK: - Against the span before

    private func comparisonSection(_ report: HabitsReport) -> some View {
        Section {
            Grid(alignment: .leading, horizontalSpacing: 8, verticalSpacing: 8) {
                GridRow {
                    Text("")
                    Text("Weekdays").gridColumnAlignment(.trailing)
                    Text("Weekends").gridColumnAlignment(.trailing)
                }
                .font(.caption)
                .foregroundStyle(Color(.secondaryLabel))

                ForEach(HabitsComparison.of(report)) { row in
                    GridRow {
                        Text(row.measure.name).font(.subheadline)
                        figure(row.weekdays, row.measure)
                        figure(row.weekends, row.measure)
                    }
                    .accessibilityElement(children: .combine)
                }
            }
            .padding(.vertical, 4)
        } header: {
            Text("Mean day")
        } footer: {
            Text(comparisonFooter(report))
        }
    }

    /// The span's figure over its change from the span before, so a row is two short columns
    /// rather than four.
    private func figure(_ comparison: SpanComparison, _ measure: HabitsComparison.Measure) -> some View {
        VStack(alignment: .trailing, spacing: 0) {
            value(comparison.recent, measure)
            change(comparison, measure)
        }
    }

    private func value(_ value: Double?, _ measure: HabitsComparison.Measure) -> some View {
        Text(value.map { Self.text($0, measure) } ?? "—")
            .font(.subheadline.weight(.semibold).monospacedDigit())
    }

    private func change(_ comparison: SpanComparison, _ measure: HabitsComparison.Measure) -> some View {
        Text(comparison.change.map { Self.changeText($0, measure) } ?? "")
            .font(.caption.monospacedDigit())
            .foregroundStyle(HabitsComparison.isWorse(comparison, measure.polarity) ? .orange : Color(.secondaryLabel))
    }

    static func text(_ value: Double, _ measure: HabitsComparison.Measure) -> String {
        switch measure {
        case .steps: value.formatted(.number.precision(.fractionLength(0)))
        case .longSittings: value.formatted(.number.precision(.fractionLength(1)))
        case .bedtime: Formatting.clockTime(minutesAfterMidnight: value + 720)
        case .wakeTime: Formatting.clockTime(minutesAfterMidnight: value)
        case .activeMinutes, .sittingMinutes, .longestSitting: Formatting.hoursAndMinutes(seconds: value * 60)
        case .bedtimeSpread, .wakeSpread: "±\(Int(value.rounded())) min"
        }
    }

    static func changeText(_ change: Double, _ measure: HabitsComparison.Measure) -> String {
        switch measure {
        case .steps: Formatting.change(change)
        case .longSittings: Formatting.change(change, fractionDigits: 1)
        case .activeMinutes, .sittingMinutes, .longestSitting, .bedtime, .wakeTime, .bedtimeSpread, .wakeSpread:
            "\(Formatting.change(change)) min"
        }
    }

    private func comparisonFooter(_ report: HabitsReport) -> String {
        let current = report.current
        let previous = report.previous
        let days = "\(current.weekdays.completeDays) weekdays and \(current.weekends.completeDays) weekend days, against \(previous.weekdays.completeDays) and \(previous.weekends.completeDays) in the \(span.days) days before."
        let jetLag = SocialJetLag.minutes(current).map {
            " Weekend sleep midpoint \(Formatting.change($0)) min from weekdays."
        } ?? ""
        return days + jetLag + " Orange: a change in the worse direction."
    }

    // MARK: - By hour

    @ViewBuilder
    private func hoursSection(_ report: HabitsReport) -> some View {
        let figures = dayKind == .weekdays ? report.current.weekdays : report.current.weekends
        let segments = HourSegment.of(figures.hours)
        Section {
            Picker("Days", selection: $dayKind) {
                ForEach(DayKind.allCases, id: \.self) { kind in Text(kind.label).tag(kind) }
            }
            .pickerStyle(.segmented)

            Chart(segments) { segment in
                BarMark(x: .value("Hour", Double(segment.position)), y: .value("Minutes", segment.minutes), width: .fixed(barWidth))
                    .foregroundStyle(segment.kind.colour)
            }
            .chartXScale(domain: -0.5...23.5)
            .chartYScale(domain: 0...60)
            .chartXAxis { hourAxis }
            .chartYAxis {
                AxisMarks(values: [0, 30, 60]) { value in
                    AxisGridLine()
                    AxisValueLabel { if let minutes = value.as(Int.self) { Text("\(minutes)") } }
                }
            }
            .frame(height: chartHeight)
            .accessibilityLabel("The mean \(dayKind.label.lowercased().dropLast()) by hour")
            .accessibilityValue(hoursSummary(figures))

            Chart(figures.hours) { hour in
                BarMark(x: .value("Hour", Double(HourSegment.position(of: hour.hour))), y: .value("Sessions", hour.sessionsStarted), width: .fixed(barWidth))
                    .foregroundStyle(Color(.label))
            }
            .chartXScale(domain: -0.5...23.5)
            .chartXAxis { hourAxis }
            // Labelled like the chart above, so the two plots are the same width and the hours align.
            .chartYAxis { AxisMarks(values: .automatic(desiredCount: 2)) }
            .frame(height: smallChartHeight)
            .accessibilityLabel("Training sessions begun by hour")
            .accessibilityValue(trainingSummary(figures))

            legend
        } header: {
            Text("When in the day")
        } footer: {
            Text("Top: the mean complete day, minutes in each hour from 04:00. Bottom: training sessions begun in each hour across the span.")
        }
    }

    private var hourAxis: some AxisContent {
        AxisMarks(values: [0.0, 4, 8, 12, 16, 20]) { value in
            AxisGridLine()
            AxisValueLabel {
                if let position = value.as(Double.self) {
                    Text(String(format: "%02d", (Int(position) + 4) % 24))
                }
            }
        }
    }

    private var legend: some View {
        ViewThatFits(in: .horizontal) {
            HStack(spacing: 12) { legendItems }
            VStack(alignment: .leading, spacing: 4) { legendItems }
        }
        .font(.caption)
        .foregroundStyle(Color(.secondaryLabel))
        .accessibilityHidden(true)
    }

    @ViewBuilder
    private var legendItems: some View {
        ForEach(HourSegment.Kind.allCases.reversed(), id: \.self) { kind in
            HStack(spacing: 4) {
                RoundedRectangle(cornerRadius: 2).fill(kind.colour).frame(width: 10, height: 10)
                Text(kind.name)
            }
        }
    }

    private func hoursSummary(_ figures: HabitsFigures) -> String {
        let busiest = figures.hours.max { ($0.low + $0.medium + $0.high) < ($1.low + $1.medium + $1.high) }
        let stillest = figures.hours.max { $0.sitting < $1.sitting }
        return [
            busiest.map { "Most active hour \(String(format: "%02d:00", $0.hour))" },
            stillest.map { "most sitting at \(String(format: "%02d:00", $0.hour))" },
        ].compactMap(\.self).joined(separator: ", ")
    }

    private func trainingSummary(_ figures: HabitsFigures) -> String {
        figures.hours.filter { $0.sessionsStarted > 0 }
            .map { "\($0.sessionsStarted) at \(String(format: "%02d:00", $0.hour))" }
            .joined(separator: ", ")
    }

    // MARK: - Sitting

    @ViewBuilder
    private func sittingSection(_ report: HabitsReport) -> some View {
        let points = report.days.filter(\.isComplete).compactMap { day in
            day.longestSittingMinutes.map { MetricPoint(day: day.day, value: Double($0)) }
        }
        if points.count >= 2 {
            Section {
                Chart {
                    RuleMark(y: .value("An hour", 60))
                        .lineStyle(StrokeStyle(lineWidth: 1, dash: [3, 3]))
                        .foregroundStyle(Color(.tertiaryLabel))
                    ForEach(points) { point in
                        PointMark(x: .value("Day", point.day.middayDate()), y: .value("Minutes", point.value))
                            .symbolSize(span == .year ? 8 : 18)
                            .foregroundStyle(Color(.tertiaryLabel))
                    }
                    ForEach(Array(MetricTrend.unbrokenRuns(of: MetricTrend.rollingMean(of: points)).enumerated()), id: \.offset) { run, stretch in
                        ForEach(stretch) { point in
                            LineMark(
                                x: .value("Day", point.day.middayDate()),
                                y: .value("Seven-day mean", point.value),
                                series: .value("Stretch", run))
                                .interpolationMethod(.monotone)
                                .foregroundStyle(Color(.label))
                        }
                    }
                }
                .chartXAxis { dayAxis }
                .frame(height: chartHeight)
                .padding(.vertical, 6)
                .accessibilityLabel("Longest sitting each day")
            } header: {
                Text("Longest sitting, minutes")
            } footer: {
                Text("Each day's longest unbroken waking sitting; the line is the seven-day mean, the dashed rule an hour. Broken by any five minutes of movement.")
            }
        }
    }

    // MARK: - Sleep timing

    @ViewBuilder
    private func sleepSection(_ report: HabitsReport) -> some View {
        let nights = report.days.compactMap { day -> (CalendarDay, Double, Double)? in
            guard let bedtime = day.bedtime, let wake = day.wakeTime else { return nil }
            // Both on the axis that starts at noon the day before, so a night is one bar.
            return (day.day, bedtime.minutesAfterNoon, wake.minutesAfterMidnight + 720)
        }
        if nights.count >= 2 {
            Section {
                Chart(nights, id: \.0) { night in
                    BarMark(
                        x: .value("Day", night.0.middayDate(), unit: .day),
                        yStart: .value("Bedtime", night.1),
                        yEnd: .value("Wake", night.2),
                        width: .ratio(0.6))
                        .foregroundStyle(SleepStage.light.colour.opacity(0.75))
                }
                .chartYScale(domain: .automatic(includesZero: false, reversed: true))
                .chartYAxis {
                    AxisMarks(values: .stride(by: 120)) { value in
                        AxisGridLine()
                        AxisValueLabel {
                            if let minutes = value.as(Double.self) {
                                Text(Formatting.clockTime(minutesAfterMidnight: minutes + 720))
                            }
                        }
                    }
                }
                .chartXAxis { dayAxis }
                .frame(height: chartHeight)
                .padding(.vertical, 6)
                .accessibilityLabel("Bedtime to wake, each night")

                if let timing = report.current.allDays.sleep {
                    FigureGrid(figures: [
                        OuraPanel.Figure(label: "Bedtime", value: Formatting.clockTime(minutesAfterMidnight: timing.bedtimeMinutesAfterNoon + 720)),
                        OuraPanel.Figure(label: "Wake", value: Formatting.clockTime(minutesAfterMidnight: timing.wakeMinutesAfterMidnight)),
                        OuraPanel.Figure(label: "Bedtime spread", value: "±\(Int(timing.bedtimeSpreadMinutes.rounded())) min"),
                        OuraPanel.Figure(label: "Wake spread", value: "±\(Int(timing.wakeSpreadMinutes.rounded())) min"),
                    ])
                }
            } header: {
                Text("Sleep timing")
            } footer: {
                Text("Each night's main sleep, bedtime to wake, filed under the morning it ended. Spread is the standard deviation across nights.")
            }
        }
    }

    // MARK: - Steps

    @ViewBuilder
    private func stepsSection(_ report: HabitsReport) -> some View {
        let days = report.days.filter(\.isComplete)
        if days.count >= 2 {
            Section {
                Chart {
                    if let mean = report.current.allDays.steps {
                        RuleMark(y: .value("Mean", mean))
                            .lineStyle(StrokeStyle(lineWidth: 1, dash: [3, 3]))
                            .foregroundStyle(Color(.tertiaryLabel))
                    }
                    ForEach(days) { day in
                        BarMark(x: .value("Day", day.day.middayDate(), unit: .day), y: .value("Steps", day.steps))
                            .foregroundStyle(day.isWeekend ? Color(.systemGray3) : Color(.systemGray))
                    }
                }
                .chartXAxis { dayAxis }
                .frame(height: chartHeight)
                .padding(.vertical, 6)
                .accessibilityLabel("Steps each day")
                .accessibilityValue(report.current.allDays.steps.map { "Mean \(Int($0))" } ?? "")
            } header: {
                Text("Steps")
            } footer: {
                Text("Lighter bars are weekends; the dashed rule is the span's mean.")
            }
        }
    }

    private var dayAxis: some AxisContent {
        AxisMarks(values: .automatic(desiredCount: 4)) { _ in
            AxisGridLine()
            AxisValueLabel(format: span == .year ? .dateTime.month(.abbreviated) : .dateTime.month(.abbreviated).day())
        }
    }

    // MARK: - Days

    @ViewBuilder
    private func daysSection(_ report: HabitsReport) -> some View {
        // Days with a classification, newest first — a day without one has no timeline to open.
        let classified = report.days.reversed().filter { $0.longestSittingMinutes != nil }
        Section("Days") {
            if classified.isEmpty {
                Text("No day in this span has a five-minute classification.").foregroundStyle(Color(.secondaryLabel))
            }
            ForEach(classified.prefix(31)) { day in
                NavigationLink(value: BodyRoute.dayActivity(day.day)) {
                    LabeledContent {
                        Text(day.longestSittingMinutes.map { "longest \(Formatting.minutes(Double($0)))" } ?? "")
                            .font(.subheadline.monospacedDigit())
                            .foregroundStyle(Color(.secondaryLabel))
                    } label: {
                        VStack(alignment: .leading, spacing: 2) {
                            Text(Formatting.date(day.day)).font(.subheadline)
                            Text("\(day.steps.formatted()) steps\(day.isComplete ? "" : " · incomplete")")
                                .font(.caption.monospacedDigit())
                                .foregroundStyle(Color(.secondaryLabel))
                        }
                    }
                }
            }
        }
    }
}
