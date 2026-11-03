import Charts
import SwiftUI

/// Is the aerobic base improving? The figures that answer it, then the weeks, the estimates and
/// the sessions behind them.
///
/// Every span and comparison says what it is in words: the aerobic base is always the last four
/// weeks against the eight before, so it sits above the span control it does not depend on, and
/// the control says which days it covers. How zones are set and what is left out is one tap away,
/// in the method sheet, rather than spread through the footers.
///
/// Its own screen under Body rather than a section of it, because cardio is a question of its own —
/// trail running and better cardio are a stated goal — with its own span, its own heart-rate
/// method to state, and sessions to open. Everything on it is the server's cardio report; this
/// screen chooses words and charts.
struct CardioView: View {
    let store: CardioStore

    @State private var span: TrendSpan = .quarter
    @State private var selectedWeek: Date?
    @State private var isMethodShown = false

    @ScaledMetric(relativeTo: .body) private var chartHeight: CGFloat = 190
    @ScaledMetric(relativeTo: .body) private var trendHeight: CGFloat = 120
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    @ScaledMetric(relativeTo: .caption) private var legendWidth: CGFloat = 72

    var body: some View {
        List {
            if let report = store.reports[span]?.value {
                aerobicBaseSection(report)
            }
            Section {
                Picker("Span", selection: $span) {
                    ForEach(TrendSpan.allCases) { span in
                        Text(span.label).tag(span)
                    }
                }
                .pickerStyle(.segmented)
                .listRowBackground(Color.clear)
                .listRowInsets(EdgeInsets())
            } footer: {
                Text("\(Formatting.spanCaption(span, endingOn: store.today)). The weeks, estimates and sessions below cover these days.")
            }

            switch store.reports[span] ?? .loading {
            case .loading:
                Section { SkeletonRows(count: 5) }
            case .unavailable(let failure):
                ContentUnavailableView {
                    Text("Cardio not available")
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
        .navigationTitle("Cardio")
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button("How this is worked out", systemImage: "info.circle") { isMethodShown = true }
            }
        }
        .sheet(isPresented: $isMethodShown) {
            CardioMethodSheet(report: store.reports[span]?.value)
        }
        .refreshable { await store.load(span) }
        .task(id: span) {
            if store.reports[span]?.value == nil { await store.load(span) }
        }
    }

    @ViewBuilder
    private func content(_ report: CardioReport, staleBecause staleness: Staleness?) -> some View {
        if let staleness { StaleBanner(staleness) }

        weeksSection(report)
        estimatesSection(report)
        sessionsSection(report)
        Section {
            Button("How zones are set, and what is left out") { isMethodShown = true }
        }
    }

    // MARK: - Aerobic base

    @ViewBuilder
    private func aerobicBaseSection(_ report: CardioReport) -> some View {
        if dynamicTypeSize.isAccessibilitySize {
            // Four columns cannot hold at these sizes; each figure becomes its own row instead.
            Section {
                ForEach(AerobicBaseFigure.of(report.aerobicBase)) { figure in
                    VStack(alignment: .leading, spacing: 2) {
                        Text(figure.measure.name)
                        Text(stackedLine(figure))
                            .font(.subheadline.monospacedDigit())
                            .foregroundStyle(figure.direction == .worse ? .orange : Color(.secondaryLabel))
                    }
                    .accessibilityElement(children: .combine)
                }
            } header: {
                Text(Self.aerobicBaseTitle)
            } footer: {
                Text(aerobicBaseFooter(report))
            }
        } else {
            aerobicBaseTable(report)
        }
    }

    private func stackedLine(_ figure: AerobicBaseFigure) -> String {
        let digits = digits(figure.measure)
        func text(_ value: Double?) -> String {
            value.map { $0.formatted(.number.precision(.fractionLength(digits))) } ?? "—"
        }
        let change = figure.comparison.change.map { ", \(Formatting.change($0, fractionDigits: digits))" } ?? ""
        return "Last 4 weeks \(text(figure.comparison.recent)) \(figure.measure.unit), the 8 before \(text(figure.comparison.before))\(change)"
    }

    private func aerobicBaseTable(_ report: CardioReport) -> some View {
        Section {
            Grid(alignment: .leading, horizontalSpacing: 10, verticalSpacing: 8) {
                GridRow {
                    Text("")
                    Text("Last 4\nweeks").gridColumnAlignment(.trailing)
                    Text("8 weeks\nbefore").gridColumnAlignment(.trailing)
                    Text("Change").gridColumnAlignment(.trailing)
                }
                .multilineTextAlignment(.trailing)
                .font(.caption)
                .foregroundStyle(Color(.secondaryLabel))

                ForEach(AerobicBaseFigure.of(report.aerobicBase)) { figure in
                    GridRow {
                        VStack(alignment: .leading, spacing: 0) {
                            Text(figure.measure.name).font(.subheadline)
                            Text(figure.measure.isEstimate ? "\(figure.measure.unit), Oura estimate" : figure.measure.unit)
                                .font(.caption2)
                                .foregroundStyle(Color(.secondaryLabel))
                        }
                        figureText(figure.comparison.recent, figure.measure).fontWeight(.semibold)
                        figureText(figure.comparison.before, figure.measure)
                            .foregroundStyle(Color(.secondaryLabel))
                        Text(figure.comparison.change.map { Formatting.change($0, fractionDigits: digits(figure.measure)) } ?? "—")
                            .font(.subheadline.monospacedDigit())
                            .foregroundStyle(figure.direction == .worse ? .orange : Color(.label))
                    }
                    .accessibilityElement(children: .combine)
                }
            }
            .padding(.vertical, 4)
        } header: {
            Text(Self.aerobicBaseTitle)
        } footer: {
            Text(aerobicBaseFooter(report))
        }
    }

    private static let aerobicBaseTitle = "Aerobic base: last 4 weeks against the 8 before"

    private func figureText(_ value: Double?, _ measure: AerobicBaseFigure.Measure) -> some View {
        Text(value.map { $0.formatted(.number.precision(.fractionLength(digits(measure)))) } ?? "—")
            .font(.subheadline.monospacedDigit())
    }

    private func digits(_ measure: AerobicBaseFigure.Measure) -> Int {
        switch measure {
        case .restingHeartRate: 1
        case .zone2MinutesPerWeek, .cardioMinutesPerWeek, .vo2Max, .cardiovascularAge: 0
        }
    }

    private func aerobicBaseFooter(_ report: CardioReport) -> String {
        let base = report.aerobicBase
        let windows = "Weekly means. The last 4 weeks are \(Formatting.span(from: base.recent.from, to: base.recent.to)); the 8 before, \(Formatting.span(from: base.before.from, to: base.before.to)). These do not change with the span below."
        let change = " Change is the last 4 weeks minus the 8 before; orange when it went the worse way."
        let unknown = base.zone2MinutesPerWeek.recent == nil
            ? " Zone 2 is blank where sessions exist but no heart rate is synced for them."
            : ""
        return windows + change + unknown
    }

    // MARK: - Weeks

    @ViewBuilder
    private func weeksSection(_ report: CardioReport) -> some View {
        let segments = ZoneSegment.of(report.weeks)
        Section {
            weekReadout(report)
            Chart {
                ForEach(segments) { segment in
                    BarMark(
                        x: .value("Week", segment.weekStarting.middayDate(), unit: .weekOfYear),
                        y: .value("Minutes", segment.minutes))
                        .foregroundStyle(ZoneColour.of(segment.zone))
                }
                ForEach(report.weeks.filter { $0.minutes > $0.zones.inZones }) { week in
                    // The rest of the sessions' time, uncoloured: below zone 1, between samples,
                    // or with no heart rate synced. The bar's height is always the time trained.
                    BarMark(
                        x: .value("Week", week.weekStarting.middayDate(), unit: .weekOfYear),
                        y: .value("Minutes", week.minutes - week.zones.inZones))
                        .foregroundStyle(Color(.systemGray4))
                }
                if let selectedWeek {
                    RuleMark(x: .value("Selected", selectedWeek)).foregroundStyle(Color(.separator))
                }
            }
            .chartXSelection(value: $selectedWeek)
            .chartXAxis {
                AxisMarks(values: .automatic(desiredCount: 4)) { _ in
                    AxisGridLine()
                    AxisValueLabel(format: .dateTime.month(.abbreviated).day())
                }
            }
            .frame(height: chartHeight)
            .padding(.vertical, 6)
            .accessibilityLabel("Cardio minutes by week, by heart-rate zone")
            .accessibilityValue(weeksSummary(report))

            zoneLegend(report)
        } header: {
            Text("Minutes a week, by heart-rate zone")
        } footer: {
            Text("One bar a week, Monday to Sunday: its height the time in sessions, coloured by zone. Grey is time below zone 1, between samples, or with no heart rate synced. Press and drag across the chart to read one week.")
        }
    }

    private func weekReadout(_ report: CardioReport) -> some View {
        let week = selectedWeek.flatMap { date in
            report.weeks.min { abs($0.weekStarting.middayDate().timeIntervalSince(date)) < abs($1.weekStarting.middayDate().timeIntervalSince(date)) }
        }
        let shown = week.map { [$0] } ?? report.weeks
        let caption = week.map { "Week of \(Formatting.date($0.weekStarting))" } ?? "A mean week, last \(span.days) days"
        // Per week of days actually in the span, so a part-week at either end does not read as a
        // light one.
        let weeks = max(1, Double(shown.reduce(0) { $0 + $1.daysInRange }) / 7)
        let figures: [(label: String, value: String)] = [
            ("Sessions", Self.number(Double(shown.reduce(0) { $0 + $1.sessions }) / weeks, digits: week == nil ? 1 : 0)),
            ("Minutes", Self.number(shown.reduce(0) { $0 + $1.minutes } / weeks)),
            ("Zone 2 min", Self.number(shown.reduce(0) { $0 + $1.zones.zone2 } / weeks)),
        ]
        return VStack(alignment: .leading, spacing: 6) {
            Text(caption).font(.subheadline).foregroundStyle(Color(.secondaryLabel))
            SummaryFigures(figures: figures)
        }
    }

    private static func number(_ value: Double, digits: Int = 0) -> String {
        value.formatted(.number.precision(.fractionLength(digits)))
    }

    private func zoneLegend(_ report: CardioReport) -> some View {
        let floors = report.maxHeartRate?.zoneFloors ?? []
        return LazyVGrid(columns: [GridItem(.adaptive(minimum: legendWidth), alignment: .leading)], alignment: .leading, spacing: 4) {
            legendItems(floors)
        }
        .font(.caption.monospacedDigit())
        .foregroundStyle(Color(.secondaryLabel))
        .accessibilityElement(children: .combine)
    }

    @ViewBuilder
    private func legendItems(_ floors: [Int]) -> some View {
        ForEach(1...5, id: \.self) { zone in
            HStack(spacing: 4) {
                RoundedRectangle(cornerRadius: 2).fill(ZoneColour.of(zone)).frame(width: 10, height: 10)
                Text(floors.count == 5 ? "Z\(zone) \(floors[zone - 1])+" : "Z\(zone)")
            }
        }
    }

    private func weeksSummary(_ report: CardioReport) -> String {
        let minutes = report.weeks.reduce(0) { $0 + $1.minutes }
        let zone2 = report.weeks.reduce(0) { $0 + $1.zones.zone2 }
        return "\(Formatting.minutes(minutes)) across \(report.weeks.count) weeks, \(Formatting.minutes(zone2)) in zone 2."
    }

    // MARK: - Oura's estimates

    @ViewBuilder
    private func estimatesSection(_ report: CardioReport) -> some View {
        // Oura has issued no VO₂max for this athlete; a section saying so for every span is noise.
        if !report.vo2Max.isEmpty {
            trendSection(
                "VO₂max, ml/kg/min", points: report.vo2Max, smooths: false,
                footer: "Oura's estimate from heart rate and walking, not a test, issued on the days it has walking to work from. Read the trend, not the number.")
        }
        trendSection(
            "Lowest night heart rate, bpm", points: report.restingHeartRate, smooths: true,
            footer: "From each night's main sleep; the line is the seven-day mean. A falling resting rate is the slowest-moving sign of a growing aerobic base.")
        trendSection(
            "Cardiovascular age, years", points: report.cardiovascularAge, smooths: false,
            footer: "Oura's prediction from pulse-wave velocity: a model's estimate, not a measurement.")
    }

    @ViewBuilder
    /// One of Oura's figures over the span. A daily figure is drawn as points under its seven-day
    /// mean; an estimate Oura issues only now and then is drawn as its own line, since a seven-day
    /// mean of two readings a week is no mean at all.
    private func trendSection(_ title: String, points: [DayFigure], smooths: Bool, footer: String) -> some View {
        Section {
            if points.count >= 2 {
                Chart {
                    ForEach(points) { point in
                        PointMark(x: .value("Day", point.day.middayDate()), y: .value("Value", point.value))
                            .symbolSize(span == .year ? 8 : 16)
                            .foregroundStyle(Color(.tertiaryLabel))
                    }
                    let daily = points.map { MetricPoint(day: $0.day, value: $0.value) }
                    let mean = smooths ? MetricTrend.rollingMean(of: daily) : daily
                    ForEach(Array(MetricTrend.unbrokenRuns(of: mean).enumerated()), id: \.offset) { run, stretch in
                        ForEach(stretch) { point in
                            LineMark(
                                x: .value("Day", point.day.middayDate()),
                                y: .value("Seven-day mean", point.value),
                                series: .value("Stretch", run))
                                .interpolationMethod(.monotone)
                                .foregroundStyle(Tokens.accent)
                        }
                    }
                }
                .chartYScale(domain: .automatic(includesZero: false))
                .chartXAxis {
                    AxisMarks(values: .automatic(desiredCount: 4)) { _ in
                        AxisGridLine()
                        AxisValueLabel(format: .dateTime.month(.abbreviated).day())
                    }
                }
                .frame(height: trendHeight)
                .padding(.vertical, 6)
                .accessibilityLabel(title)
                .accessibilityValue(points.last.map { "Latest \(MeasurementText.plain($0.value))" } ?? "")
            } else {
                Text(points.isEmpty ? "None stored in this span." : "One reading in this span: \(MeasurementText.plain(points[0].value)).")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        } header: {
            Text(title)
        } footer: {
            Text(footer)
        }
    }

    // MARK: - Sessions

    @ViewBuilder
    private func sessionsSection(_ report: CardioReport) -> some View {
        Section {
            if report.sessions.isEmpty {
                Text("No cardio sessions in this span.").foregroundStyle(Color(.secondaryLabel))
            }
            ForEach(report.sessions.reversed()) { session in
                NavigationLink(value: BodyRoute.cardioSession(session.id, span)) {
                    CardioSessionRow(session: session)
                }
            }
        } header: {
            Text("Sessions")
        } footer: {
            if !report.excluded.isEmpty {
                Text("Left out: " + report.excluded
                    .map { "\($0.count) \($0.reason.phrase)" }
                    .joined(separator: ", ") + ".")
            }
        }
    }

}

/// How the cardio screen's figures are worked out: the zones, what counts as a session, and the
/// aerobic base — the method, in one place, opened from the screen's info button.
private struct CardioMethodSheet: View {
    /// The report on screen, for the maximum heart rate in use and what was left out. `nil` before
    /// one has loaded, when the sheet states the method alone.
    let report: CardioReport?

    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            List {
                Section("Heart-rate zones") {
                    Text(Self.zones(report?.maxHeartRate))
                }
                Section("What counts as a session") {
                    Text("Your logged runs and distance work, and the workouts the ring recorded. A ring workout is left out when it is housework, yard work or stretching, a walk under 30 minutes, strength work, or at least half inside a logged session — which is that session as the ring saw it.")
                    if let excluded = report?.excluded, !excluded.isEmpty {
                        Text("Left out in this span: " + excluded.map { "\($0.count) \($0.reason.phrase)" }.joined(separator: ", ") + ".")
                            .foregroundStyle(Color(.secondaryLabel))
                    }
                    Text("Each session's heart rate is the ring's all-day heart rate across its clock time. A session with no samples synced has no zones.")
                }
                Section("Aerobic base") {
                    Text("Five figures, each the last 4 weeks against the 8 weeks before them: zone 2 minutes a week, cardio minutes a week, VO₂max, the nights' lowest heart rate, and cardiovascular age. More zone 2 and cardio time, a higher VO₂max and a lower resting rate are the better direction. VO₂max and cardiovascular age are Oura's estimates, not tests.")
                }
            }
            .listStyle(.insetGrouped)
            .navigationTitle("How cardio is worked out")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Done") { dismiss() }
                }
            }
        }
    }

    private static func zones(_ maximum: MaxHeartRate?) -> String {
        let scheme = "Five zones at 50, 60, 70, 80 and 90% of maximum heart rate; zone 2 is 60–70%."
        switch maximum {
        case .agePredicted(let bpm, let age, let peak)?:
            let observed = peak.map { "The ring's own cardio peak over the last year is \($0) bpm — below the prediction, and as likely the finger sensor under-reading hard efforts as the heart, so it is not used." }
                ?? "Too few cardio heart-rate samples yet to read the ring's own peak."
            return "\(scheme) Maximum \(bpm) bpm, predicted from age \(age) (Tanaka: 208 − 0.7 × age, about ±10 bpm for any one person). \(observed)"
        case .observed(let bpm, let predicted, let age)?:
            return "\(scheme) Maximum \(bpm) bpm: the ring's own cardio peak over the last year (99th percentile of its samples), above the \(predicted) bpm predicted from age \(age). A rate actually reached cannot exceed the true maximum, so the higher figure is used."
        case .unrecognised?:
            return scheme
        case nil:
            return "\(scheme) No zones: the profile has no usable age and the ring no peak to read."
        }
    }
}

/// One cardio session as a row: what, when, how long, and its zones as one stacked bar.
struct CardioSessionRow: View {
    let session: CardioSession

    var body: some View {
        VStack(alignment: .leading, spacing: 5) {
            HStack(alignment: .firstTextBaseline) {
                Text(session.activityName).font(.subheadline.weight(.semibold))
                Spacer()
                Text(session.start, format: .dateTime.month(.abbreviated).day().hour().minute())
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
            Text(facts)
                .font(.caption.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
            if case .recorded(_, _, _, let zones?) = session.heartRate, zones.inZones > 0 {
                ZoneBar(zones: zones).frame(height: 6)
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }

    private var facts: String {
        var parts = [Formatting.minutes(session.minutes)]
        if let meters = session.distanceMeters, meters >= 1 { parts.append(OuraWorkout.distance(meters: meters)) }
        switch session.heartRate {
        case .recorded(let average, let peak, _, let zones):
            parts.append("avg \(average) · peak \(peak) bpm")
            if let zones, zones.zone2 >= 1 { parts.append("Z2 \(Formatting.minutes(zones.zone2))") }
        case .notRecorded:
            parts.append("no heart rate synced")
        case .unrecognised:
            break
        }
        return parts.joined(separator: " · ")
    }
}

/// A session's zone minutes as one bar, zone 1 to 5 left to right.
struct ZoneBar: View {
    let zones: ZoneMinutes

    var body: some View {
        GeometryReader { proxy in
            HStack(spacing: 1) {
                ForEach(Array(zones.byZone.enumerated()), id: \.offset) { index, minutes in
                    if minutes > 0 {
                        Rectangle()
                            .fill(ZoneColour.of(index + 1))
                            .frame(width: max(1, proxy.size.width * minutes / max(1, zones.inZones) - 1))
                    }
                }
            }
        }
        .clipShape(.capsule)
        .accessibilityHidden(true)
    }
}
