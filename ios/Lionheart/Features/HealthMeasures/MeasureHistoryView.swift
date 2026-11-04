import Charts
import SwiftUI

/// One Apple Health figure, as one device measured it, over a year, three, or everything Health holds.
///
/// The span ends on the figure's last measured day rather than today, so a figure the watch stopped
/// measuring when it was last worn still opens onto its own history rather than an empty year.
/// Weekly means are the line; within a year each day is a faint point beneath it.
struct MeasureHistoryView: View {
    let metric: HealthMetric

    @Environment(HealthMeasuresStore.self) private var store

    @State private var span: HistorySpan = .year
    @State private var selectedDate: Date?
    @State private var isExplained = false

    @ScaledMetric(relativeTo: .body) private var chartHeight: CGFloat = 220

    var body: some View {
        List {
            switch store.state {
            case .loading:
                Section { SkeletonRows(count: 3) }
            case .unavailable(let failure):
                ContentUnavailableView {
                    Text("Not available")
                } description: {
                    Text(failure.message)
                } actions: {
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await store.refresh() } }
                    }
                }
                .listRowBackground(Color.clear)
            case .loaded(let history):
                content(history)
            case .stale(let history, let staleness):
                StaleBanner(staleness)
                content(history)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle(metric.title)
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button("What this is", systemImage: "info.circle") { isExplained = true }
            }
        }
        .sheet(isPresented: $isExplained) {
            MeasureInfoSheet(metrics: [metric], title: metric.title)
                .presentationDetents([.medium, .large])
        }
        .refreshable { await store.refresh() }
        .task { await store.refreshIfNeeded() }
    }

    @ViewBuilder
    private func content(_ history: HealthMeasureHistory) -> some View {
        if let principal = history.principal(metric), let last = principal.days.last {
            let from = span.days.map { last.day.adding(days: 1 - $0) } ?? principal.days[0].day
            let days = principal.days.filter { $0.day >= from }
            let weeks = WeekMean.of(days)

            Section {
                Picker("Span", selection: $span) {
                    ForEach(HistorySpan.allCases) { Text($0.label).tag($0) }
                }
                .pickerStyle(.segmented)
                .listRowBackground(Color.clear)
                .listRowInsets(EdgeInsets())
            }

            Section {
                readout(days: days, weeks: weeks)
                chart(days: days, weeks: weeks)
            } header: {
                Text("\(principal.device.name) · \(Formatting.date(from)) to \(Formatting.date(last.day))")
            } footer: {
                Text(span == .year
                     ? "Points are days; the line is each week's mean."
                     : "The line is each week's mean.")
            }

            summary(days: days, weeks: weeks)

            Section {
                Text(MeasureInfoSheet.meaning(of: metric))
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        } else {
            ContentUnavailableView {
                Text("Not measured")
            } description: {
                Text("Health holds no \(metric.title.lowercased()) from your Apple Watch or iPhone yet.")
            }
            .listRowBackground(Color.clear)
        }
    }

    private func readout(days: [MeasuredDay], weeks: [WeekMean]) -> some View {
        let selected = selectedDate.flatMap { date in
            weeks.min { abs($0.weekStarting.middayDate().timeIntervalSince(date)) < abs($1.weekStarting.middayDate().timeIntervalSince(date)) }
        }
        let value = selected?.mean ?? Self.mean(days)
        let caption = selected.map { "Week of \(Formatting.date($0.weekStarting))" } ?? "\(span.label) mean"

        return VStack(alignment: .leading, spacing: 2) {
            Text(caption)
                .font(.subheadline)
                .foregroundStyle(Color(.secondaryLabel))
            HStack(alignment: .firstTextBaseline, spacing: 4) {
                Text(metric.number(value)).font(.title.weight(.semibold).monospacedDigit())
                Text(metric.unit).font(.subheadline).foregroundStyle(Color(.secondaryLabel))
            }
        }
        .accessibilityElement(children: .combine)
    }

    private func chart(days: [MeasuredDay], weeks: [WeekMean]) -> some View {
        Chart {
            if span == .year {
                ForEach(days) { day in
                    PointMark(x: .value("Day", day.day.middayDate()), y: .value("Value", metric.shown(day.mean)))
                        .symbolSize(8)
                        .foregroundStyle(Color(.tertiaryLabel))
                }
            }
            ForEach(weeks) { week in
                LineMark(x: .value("Week", week.weekStarting.middayDate()), y: .value("Weekly mean", metric.shown(week.mean)))
                    .interpolationMethod(.monotone)
                    .lineStyle(StrokeStyle(lineWidth: 2))
                    .foregroundStyle(Tokens.accent)
            }
            if let selectedDate {
                RuleMark(x: .value("Selected", selectedDate)).foregroundStyle(Color(.separator))
            }
        }
        .chartYScale(domain: .automatic(includesZero: false))
        .chartXSelection(value: $selectedDate)
        .chartXAxis {
            AxisMarks(values: .automatic(desiredCount: 4)) { value in
                AxisGridLine()
                AxisValueLabel {
                    if let date = value.as(Date.self) {
                        Text(date, format: span == .year ? .dateTime.month(.abbreviated) : .dateTime.month(.abbreviated).year(.twoDigits))
                    }
                }
            }
        }
        .frame(height: chartHeight)
        .padding(.vertical, 8)
        .accessibilityLabel("\(metric.title) by week")
        .accessibilityValue(weeks.last.map { "Latest week \(metric.text($0.mean))" } ?? "")
    }

    @ViewBuilder
    private func summary(days: [MeasuredDay], weeks: [WeekMean]) -> some View {
        Section {
            if let lowest = weeks.min(by: { $0.mean < $1.mean }) {
                LabeledContent("Lowest week") { figure(lowest.mean, lowest.weekStarting) }
            }
            if let highest = weeks.max(by: { $0.mean < $1.mean }) {
                LabeledContent("Highest week") { figure(highest.mean, highest.weekStarting) }
            }
            LabeledContent("Days measured") {
                Text("\(days.count)").monospacedDigit()
            }
        }
    }

    private func figure(_ value: Double, _ week: CalendarDay) -> some View {
        Text("\(metric.text(value)) · \(Formatting.date(week))").monospacedDigit()
    }

    private static func mean(_ days: [MeasuredDay]) -> Double {
        days.isEmpty ? 0 : days.map(\.mean).reduce(0, +) / Double(days.count)
    }
}

/// How far back a figure's history screen reads.
enum HistorySpan: Int, CaseIterable, Identifiable {
    case year
    case threeYears
    case all

    var id: Int { rawValue }

    var days: Int? {
        switch self {
        case .year: 365
        case .threeYears: 3 * 365
        case .all: nil
        }
    }

    var label: String {
        switch self {
        case .year: "1 year"
        case .threeYears: "3 years"
        case .all: "All"
        }
    }
}
