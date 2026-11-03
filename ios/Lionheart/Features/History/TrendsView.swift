import Charts
import SwiftUI

/// Two readings of the log: how much training happened each week, and how one movement's top set
/// has moved.
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

    /// Which strength series is drawn. Held here because it is view state: which movement you are
    /// looking at is not a fact about the athlete's training.
    @State private var selectedSeries: String?

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
                weeklyVolumeSection
                strengthSection
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Trends")
    }

    // MARK: - Weekly load

    @ViewBuilder
    private var weeklyVolumeSection: some View {
        if !trends.weeks.isEmpty {
            Section {
                Chart(trends.weeks) { week in
                    // A week-wide bar on a week-stepped axis. A bare date would plot each week as a
                    // hairline at its first day under day labels, which reads as daily data.
                    BarMark(
                        x: .value("Week", week.start.middayDate(), unit: .weekOfYear),
                        y: .value("Sets", week.setCount))
                    .foregroundStyle(Tokens.accent)
                }
                .chartYAxisLabel("Sets")
                .chartXAxis {
                    AxisMarks(values: .stride(by: .weekOfYear, count: max(1, trends.weeks.count / 4))) { value in
                        AxisGridLine()
                        AxisValueLabel {
                            if let date = value.as(Date.self) {
                                Text(date, format: .dateTime.month(.abbreviated).day())
                            }
                        }
                    }
                }
                .frame(height: 180)
                .padding(.vertical, 8)
                .accessibilityLabel("Sets logged per week")
                .accessibilityValue(weeklyAccessibilityValue)
            } header: {
                Text("Sets per week")
            } footer: {
                Text("Weeks with no training are shown empty rather than skipped, so a break reads as a break.")
            }
        }
    }

    private var weeklyAccessibilityValue: String {
        let trained = trends.weeks.filter { $0.sessionCount > 0 }
        let total = trained.reduce(0) { $0 + $1.setCount }
        guard !trained.isEmpty else { return "No weeks with training." }
        return "\(Formatting.count(total, "set")) across \(Formatting.count(trained.count, "week")) of training."
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

                Chart(series.points) { point in
                    LineMark(
                        x: .value("Day", point.day.middayDate()),
                        y: .value("Weight", point.weight))
                    .foregroundStyle(Tokens.accent)
                    PointMark(
                        x: .value("Day", point.day.middayDate()),
                        y: .value("Weight", point.weight))
                    .foregroundStyle(Tokens.accent)
                }
                .chartYAxisLabel(series.unit.abbreviation)
                .chartYScale(domain: .automatic(includesZero: false))
                .frame(height: 180)
                .padding(.vertical, 8)
                .accessibilityLabel("Heaviest set of \(series.movementName) by session")
                .accessibilityValue(strengthAccessibilityValue(series))
            } header: {
                Text("Top set")
            } footer: {
                Text("The heaviest set of each session. A change of unit starts its own series rather than putting a cliff in this one.")
            }
        }
    }

    /// The series the picker selected, or the one with the most history when nothing is selected
    /// yet — which is the series worth defaulting to.
    private var shownSeries: StrengthSeries? {
        guard let selectedSeries,
              let chosen = trends.strength.first(where: { $0.id == selectedSeries })
        else { return trends.strength.first }
        return chosen
    }

    private func strengthAccessibilityValue(_ series: StrengthSeries) -> String {
        guard let first = series.points.first, let last = series.points.last else { return "" }
        let unit = series.unit.abbreviation
        return "From \(MeasurementText.plain(first.weight)) \(unit) to \(MeasurementText.plain(last.weight)) \(unit) over \(Formatting.count(series.points.count, "session"))."
    }
}
