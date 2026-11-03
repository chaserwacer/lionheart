import Charts
import SwiftUI

/// One record's chain: what stands now, everything it beat, and anything withdrawn — newest first,
/// with how long each one stood.
///
/// The chain is one record identity, unit included: a pounds five-rep max's history never lists
/// the kilograms ones. Where the links that stood share a scale, a step line above the list shows
/// the progression, flat while a record stood and rising when it was beaten.
struct RecordHistoryView: View {
    let key: RecordKey
    let name: String
    let store: HistoryStore

    @State private var asked: PersonalRecord?

    var body: some View {
        List {
            switch store.histories[key] ?? .loading {
            case .loading:
                Section { SkeletonRows(count: 4) }
            case .unavailable(let failure):
                ContentUnavailableView {
                    Label("History is not available", systemImage: "exclamationmark.triangle")
                } description: {
                    Text(failure.message)
                } actions: {
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await store.loadHistory(of: key) } }
                    }
                }
                .listRowBackground(Color.clear)
            case .loaded(let history):
                content(history, staleBecause: nil)
            case .stale(let history, let staleness):
                content(history, staleBecause: staleness)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle(key.kind.label)
        .refreshable { await store.loadHistory(of: key) }
        .task(id: key) { await store.loadHistory(of: key) }
        .retractionDialog(asking: $asked, name: name, store: store)
    }

    @ViewBuilder
    private func content(_ history: RecordHistory, staleBecause staleness: Staleness?) -> some View {
        if let staleness {
            StaleBanner(staleness)
        }

        if history.links.isEmpty {
            ContentUnavailableView {
                Text("No history")
            } description: {
                Text("No record of this kind has been set.")
            }
            .listRowBackground(Color.clear)
        } else {
            if !history.trend.isEmpty {
                Section { trendChart(history.trend) }
            }
            Section {
                ForEach(history.links) { link in
                    LinkRow(record: link)
                        .contextMenu { RecordMenu(record: link, name: name, ask: { asked = $0 }) }
                }
            } header: {
                Text(header)
            } footer: {
                if history.hasRetractions {
                    Text("A retracted record was withdrawn because the set behind it was not real; the one it had beaten stood again.")
                }
            }
        }
    }

    /// The subject, and the unit where the identity has one — so "pounds" is said once, here,
    /// rather than implied by which figures happen to be on screen.
    private var header: String {
        guard let unit = key.heldIn else { return name }
        return "\(name) · \(Formatting.unitName(unit).lowercased())"
    }

    // MARK: - Trend

    private func trendChart(_ trend: [RecordHistory.TrendPoint]) -> some View {
        Chart(trend) { point in
            LineMark(x: .value("Set", point.date), y: .value("Value", point.value))
                .interpolationMethod(.stepEnd)
                .foregroundStyle(Tokens.accent)
            PointMark(x: .value("Set", point.date), y: .value("Value", point.value))
                .foregroundStyle(Tokens.accent)
        }
        .chartYScale(domain: .automatic(includesZero: false))
        // Room at the trailing edge, where the standing record sits, so its date label is not cut.
        .chartXScale(range: .plotDimension(startPadding: 8, endPadding: 24))
        .chartXAxis {
            AxisMarks(values: .automatic(desiredCount: 3)) { value in
                AxisGridLine()
                AxisValueLabel {
                    if let date = value.as(Date.self) {
                        Text(date, format: .dateTime.month(.abbreviated).day())
                    }
                }
            }
        }
        .chartYAxis {
            AxisMarks { value in
                AxisGridLine()
                AxisValueLabel {
                    if let number = value.as(Double.self), let measure = trend.first?.measure {
                        Text(axisLabel(number, measure))
                    }
                }
            }
        }
        .frame(height: 180)
        .padding(.vertical, 8)
        .accessibilityLabel("\(key.kind.label) over time")
        .accessibilityValue(trendSummary(trend))
    }

    private func axisLabel(_ value: Double, _ measure: RecordHistory.TrendMeasure) -> String {
        switch measure {
        case .weight(let unit): Formatting.weight(value, unit)
        case .distance(let unit): Formatting.distance(value, unit)
        case .time: Formatting.clock(seconds: value)
        }
    }

    private func trendSummary(_ trend: [RecordHistory.TrendPoint]) -> String {
        guard let first = trend.first, let last = trend.last else { return "" }
        return "From \(Formatting.achievement(first.record.achievement)) to \(Formatting.achievement(last.record.achievement)) across \(Formatting.count(trend.count, "record"))."
    }
}

/// One link in a chain: the figure, where it stands, and how long the one before it stood.
private struct LinkRow: View {
    let record: PersonalRecord

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            LabeledContent {
                Text(Formatting.achievement(record.achievement))
                    .font(.headline.monospacedDigit())
                    .strikethrough(record.standing == .retracted)
                    .foregroundStyle(record.standing == .standing ? Color(.label) : Color(.secondaryLabel))
            } label: {
                Text(standing)
                    .foregroundStyle(tint)
            }
            if let detail = Formatting.achievementDetail(record.achievement) {
                Text(detail)
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
            Text(Formatting.recordLineage(record))
                .font(.caption)
                .foregroundStyle(Color(.secondaryLabel))
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }

    private var standing: String {
        switch record.standing {
        case .standing:
            guard let days = record.daysStanding(asOf: Date()), days > 0 else { return "Standing" }
            return "Standing · \(Formatting.count(days, "day"))"
        case .beaten: return "Beaten"
        case .retracted: return "Retracted"
        case .unrecognised: return "Unknown standing"
        }
    }

    private var tint: Color {
        switch record.standing {
        case .standing: .accentColor
        case .beaten: Color(.secondaryLabel)
        // Not red: withdrawing a record is the athlete's own correction, not a failure, and red is
        // the accent's neighbour — it would read as the standing one.
        case .retracted: Color(.secondaryLabel)
        case .unrecognised: Color(.tertiaryLabel)
        }
    }
}
