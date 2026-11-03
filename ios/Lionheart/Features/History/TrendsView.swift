import Charts
import SwiftUI

/// Two readings of the log over a chosen span: how much training happened each week, and how one
/// movement's top set has moved.
///
/// `Charts` is Apple's, so it is allowed — but only above `Core/`, which the layering check
/// enforces. Both series are plain values derived in `Core/Derived/TrainingTrend.swift`; this file
/// draws them and nothing more.
///
/// There is deliberately no tonnage chart. The server derives volume for a record and nothing for a
/// session, so a weekly tonnage figure would be a number only this screen believed — and summing a
/// kilogram set with a pound one would make it wrong as well as lonely.
struct TrendsView: View {
    let trends: TrainingTrends
    var today = CalendarDay(Date())

    /// Which strength series is drawn. Held here because it is view state: which movement you are
    /// looking at is not a fact about the athlete's training.
    @State private var selectedSeries: String?
    @State private var span: Span = .half

    @ScaledMetric(relativeTo: .body) private var chartHeight: CGFloat = 190

    /// How far back both charts reach, in weeks.
    enum Span: Int, CaseIterable, Identifiable {
        case quarter = 13
        case half = 26
        case year = 52

        var id: Int { rawValue }

        var label: String {
            switch self {
            case .quarter: "3M"
            case .half: "6M"
            case .year: "1Y"
            }
        }
    }

    var body: some View {
        List {
            if trends.isEmpty {
                ContentUnavailableView {
                    Text("No trend data")
                } description: {
                    Text("Trends are drawn from completed sessions with logged sets, and none are logged.")
                }
                .listRowBackground(Color.clear)
            } else {
                Section {
                    Picker("Span", selection: $span) {
                        ForEach(Span.allCases) { span in
                            Text(span.label).tag(span)
                        }
                    }
                    .pickerStyle(.segmented)
                    .listRowBackground(Color.clear)
                    .listRowInsets(EdgeInsets())
                }
                weeklyLoadSection
                strengthSection
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Trends")
    }

    // MARK: - Weekly load

    @ViewBuilder
    private var weeklyLoadSection: some View {
        let reach = Array(trends.weeks.suffix(span.rawValue + TrainingTrends.loadAverageWeeks - 1))
        let weeks = Array(reach.suffix(span.rawValue))
        let average = TrainingTrends.loadAverage(of: reach)

        if !weeks.isEmpty {
            Section {
                loadFigures(weeks, average: average)
                WeeklyLoadChart(weeks: weeks, average: average)
                    .frame(height: chartHeight)
                    .padding(.vertical, 8)
            } header: {
                Text("Sets per week")
            } footer: {
                Text("Bars are weeks, empty ones included; the line is the four-week average.")
            }
        }
    }

    private func loadFigures(_ weeks: [TrainingWeek], average: [TrainingTrends.WeeklyLoad]) -> some View {
        let trained = weeks.filter { $0.sessionCount > 0 }
        let sessions = weeks.reduce(0) { $0 + $1.sessionCount }
        let sets = weeks.reduce(0) { $0 + $1.setCount }

        return SummaryFigures(figures: [
            ("Sessions", "\(sessions)"),
            ("Sets", "\(sets)"),
            ("Weeks trained", "\(trained.count) of \(weeks.count)"),
        ])
    }

    // MARK: - Strength

    @ViewBuilder
    private var strengthSection: some View {
        if let series = shownSeries {
            let points = series.points.filter { $0.day >= spanStart }
            Section {
                if trends.strength.count > 1 {
                    // Bound to the series actually drawn, so the menu names it before the athlete
                    // has chosen one. The bare selection starts as `nil`, which no option is
                    // tagged with, and a menu bound to it shows no movement at all.
                    Picker("Movement", selection: Binding(
                        get: { Optional(series.id) },
                        set: { selectedSeries = $0 })
                    ) {
                        ForEach(trends.strength) { option in
                            Text(option.movementName).tag(Optional(option.id))
                        }
                    }
                    .pickerStyle(.menu)
                }

                if points.count >= StrengthSeries.minimumPoints {
                    strengthFigures(points, unit: series.unit)
                    Chart(points) { point in
                        LineMark(
                            x: .value("Day", point.day.middayDate()),
                            y: .value("Weight", point.weight))
                        .foregroundStyle(Tokens.accent)
                        PointMark(
                            x: .value("Day", point.day.middayDate()),
                            y: .value("Weight", point.weight))
                        .symbolSize(span == .year ? 10 : 18)
                        .foregroundStyle(Tokens.accent)
                    }
                    .chartYAxisLabel(series.unit.abbreviation)
                    .chartYScale(domain: .automatic(includesZero: false))
                    .frame(height: chartHeight)
                    .padding(.vertical, 8)
                    .accessibilityLabel("Heaviest set of \(series.movementName) by session")
                    .accessibilityValue(strengthAccessibilityValue(points, unit: series.unit))
                } else {
                    Text("Fewer than two sessions of \(series.movementName) in this span.")
                        .foregroundStyle(Color(.secondaryLabel))
                }
            } header: {
                Text("Top set")
            } footer: {
                Text("The heaviest set of each session. A change of unit starts its own series rather than putting a cliff in this one.")
            }
        }
    }

    /// The span's first and latest top set, and the change between them.
    private func strengthFigures(_ points: [StrengthSeries.Point], unit: WeightUnit) -> some View {
        let first = points.first
        let latest = points.last
        let best = points.max { $0.weight < $1.weight }
        let change = (latest?.weight ?? 0) - (first?.weight ?? 0)

        return SummaryFigures(figures: [
            ("Latest", latest.map { "\(Formatting.measure($0.weight)) × \($0.reps)" } ?? ""),
            ("Best", best.map { "\(Formatting.measure($0.weight)) × \($0.reps)" } ?? ""),
            ("Change", "\(change < 0 ? "−" : "+")\(Formatting.measure(abs(change))) \(unit.abbreviation)"),
        ])
    }

    /// The first day the span reaches.
    private var spanStart: CalendarDay {
        today.adding(days: -span.rawValue * 7)
    }

    /// The series the picker selected, or the one with the most history when nothing is selected
    /// yet — which is the series worth defaulting to.
    private var shownSeries: StrengthSeries? {
        guard let selectedSeries,
              let chosen = trends.strength.first(where: { $0.id == selectedSeries })
        else { return trends.strength.first }
        return chosen
    }

    private func strengthAccessibilityValue(_ points: [StrengthSeries.Point], unit: WeightUnit) -> String {
        guard let first = points.first, let last = points.last else { return "" }
        let unit = unit.abbreviation
        return "From \(MeasurementText.plain(first.weight)) \(unit) to \(MeasurementText.plain(last.weight)) \(unit) over \(Formatting.count(points.count, "session"))."
    }
}

/// A span's headline figures, side by side as columns, each a caption over its figure — or one
/// labelled row each when the columns would not fit, as at the accessibility sizes, so no figure
/// truncates.
private struct SummaryFigures: View {
    let figures: [(label: String, value: String)]

    var body: some View {
        ViewThatFits(in: .horizontal) {
            HStack(alignment: .top, spacing: 20) {
                ForEach(figures, id: \.label) { figure in
                    VStack(alignment: .leading, spacing: 2) {
                        Text(figure.label)
                            .font(.caption)
                            .foregroundStyle(Color(.secondaryLabel))
                        Text(figure.value)
                            .font(.title3.weight(.semibold).monospacedDigit())
                    }
                    .fixedSize()
                }
            }
            VStack(alignment: .leading, spacing: 4) {
                ForEach(figures, id: \.label) { figure in
                    LabeledContent(figure.label) {
                        Text(figure.value).monospacedDigit()
                    }
                }
            }
        }
        .accessibilityElement(children: .combine)
    }
}
