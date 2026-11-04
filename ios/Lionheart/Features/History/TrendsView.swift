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
    @State private var span: LogSpan = .half

    @ScaledMetric(relativeTo: .body) private var chartHeight: CGFloat = 190

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
                        ForEach(LogSpan.allCases) { span in
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
        let load = trends.load(lastWeeks: span.weeks(of: trends.weeks.count))

        if !load.weeks.isEmpty {
            Section {
                SummaryFigures(figures: [
                    ("Sessions", "\(load.sessions)"),
                    ("Sets", "\(load.sets)"),
                    ("Weeks trained", "\(load.weeksTrained) of \(load.weeks.count)"),
                ])
                WeeklyLoadChart(weeks: load.weeks, average: load.average)
                    .frame(height: chartHeight)
                    .padding(.vertical, 8)
            } header: {
                Text("Sets per week")
            } footer: {
                Text("Bars are weeks, empty ones included; the line is the four-week average.")
            }
        }
    }

    // MARK: - Strength

    @ViewBuilder
    private var strengthSection: some View {
        if let series = shownSeries {
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
                NavigationLink(value: HistoryRoute.movement(series.movementDataId)) {
                    Text("Movement history")
                }

                if let summary = series.summary(since: today.adding(days: -span.weeks(of: trends.weeks.count) * 7)) {
                    let points = summary.points
                    strengthFigures(summary, unit: series.unit)
                    Chart(points) { point in
                        LineMark(
                            x: .value("Day", point.day.middayDate()),
                            y: .value("Weight", Imperial.pounds(point.weight, from: series.unit)))
                        .foregroundStyle(Tokens.accent)
                        PointMark(
                            x: .value("Day", point.day.middayDate()),
                            y: .value("Weight", Imperial.pounds(point.weight, from: series.unit)))
                        .symbolSize(span.isLong ? 10 : 18)
                        .foregroundStyle(Tokens.accent)
                    }
                    .chartYAxisLabel(Imperial.weightLabel(series.unit))
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

    /// The span's latest and best top set, and the change in load across it.
    private func strengthFigures(_ summary: StrengthSeries.Summary, unit: WeightUnit) -> some View {
        let change = summary.change
        return SummaryFigures(figures: [
            ("Latest", "\(Formatting.weightParts(summary.latest.weight, unit).amount) × \(summary.latest.reps)"),
            ("Best", "\(Formatting.weightParts(summary.best.weight, unit).amount) × \(summary.best.reps)"),
            ("Change", Formatting.signedWeight(change, unit)),
        ])
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
        return "From \(Formatting.weight(first.weight, unit)) to \(Formatting.weight(last.weight, unit)) over \(Formatting.count(points.count, "session"))."
    }
}
