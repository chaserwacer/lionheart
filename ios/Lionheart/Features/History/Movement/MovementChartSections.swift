import Charts
import SwiftUI

/// A movement's numbers over time — estimated single, heaviest set, best set volume, or reps at a
/// load — and how many sessions a week it got.
///
/// One chart with a measure chosen above it rather than four charts stacked, so the screen holds
/// one thing to read at a time. Press on the chart to read one session out. Points are sessions;
/// the line joins them, and a session with nothing to measure leaves a gap rather than a zero.
struct MovementChartSections: View {
    let movement: MovementHistory
    let today: CalendarDay

    @State private var choice: Choice = .estimatedOneRepMax
    @State private var chosenLoad: Double?
    @State private var chosenUnit: WeightUnit?
    @State private var span: LogSpan = .all
    @State private var selectedDate: Date?

    @ScaledMetric(relativeTo: .body) private var chartHeight: CGFloat = 200
    @ScaledMetric(relativeTo: .body) private var frequencyHeight: CGFloat = 120

    /// The measure as the picker offers it; reps at load takes its load from a second picker.
    enum Choice: CaseIterable, Identifiable {
        case estimatedOneRepMax, heaviestSet, bestSetVolume, repsAtLoad

        var id: Self { self }

        var label: String {
            switch self {
            case .estimatedOneRepMax: "Estimated 1RM"
            case .heaviestSet: "Heaviest set"
            case .bestSetVolume: "Best set volume"
            case .repsAtLoad: "Reps at load"
            }
        }
    }

    var body: some View {
        let units = movement.units
        if let unit = chosenUnit.flatMap({ units.contains($0) ? $0 : nil }) ?? units.first {
            measureSection(unit: unit, units: units)
        }
        frequencySection
    }

    // MARK: - The measure

    @ViewBuilder
    private func measureSection(unit: WeightUnit, units: [WeightUnit]) -> some View {
        let loads = movement.repeatedLoads(in: unit)
        let load = chosenLoad.flatMap { loads.contains($0) ? $0 : nil } ?? loads.first
        let measure = measure(load: load)
        let start = span.start(endingOn: today)
        let points = measure.map { movement.series($0, in: unit).filter { point in start.map { point.day >= $0 } ?? true } } ?? []

        Section {
            Picker("Measure", selection: $choice) {
                ForEach(Choice.allCases) { Text($0.label).tag($0) }
            }
            if choice == .repsAtLoad {
                if loads.isEmpty {
                    Text("No load was used in more than one session.")
                        .foregroundStyle(Color(.secondaryLabel))
                } else {
                    Picker("Load", selection: Binding(get: { load ?? 0 }, set: { chosenLoad = $0 })) {
                        ForEach(loads, id: \.self) { Text(Formatting.weight($0, unit)).tag($0) }
                    }
                }
            }
            if units.count > 1 {
                Picker("Unit", selection: Binding(get: { unit }, set: { chosenUnit = $0 })) {
                    ForEach(units, id: \.self) { Text(Formatting.unitName($0)).tag($0) }
                }
                .pickerStyle(.segmented)
            }
            Picker("Span", selection: $span) {
                ForEach(LogSpan.allCases) { Text($0.label).tag($0) }
            }
            .pickerStyle(.segmented)

            if let measure, points.count >= StrengthSeries.minimumPoints {
                readout(points, measure: measure, unit: unit)
                chart(points, measure: measure, unit: unit)
                summary(points, measure: measure, unit: unit)
            } else if measure != nil {
                Text(points.isEmpty ? "Nothing to chart in this span." : "One session in this span. A line needs two.")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        } footer: {
            Text(footnote(for: measure))
        }
    }

    private func measure(load: Double?) -> MovementMeasure? {
        switch choice {
        case .estimatedOneRepMax: .estimatedOneRepMax
        case .heaviestSet: .heaviestSet
        case .bestSetVolume: .bestSetVolume
        case .repsAtLoad: load.map { MovementMeasure.repsAt(load: $0) }
        }
    }

    private func footnote(for measure: MovementMeasure?) -> String {
        switch measure {
        case .estimatedOneRepMax: "The best single each session implies, by Epley. Sets over 12 reps estimate nothing."
        case .heaviestSet: "The heaviest load each session, whatever the reps."
        case .bestSetVolume: "The largest load × reps of one set each session."
        case .repsAt: "The most reps done at this load each session."
        case nil: ""
        }
    }

    /// The pressed session, or the latest when nothing is pressed.
    private func readout(_ points: [MovementPoint], measure: MovementMeasure, unit: WeightUnit) -> some View {
        let pressed = selectedDate.flatMap { date in
            points.min { abs($0.day.middayDate().timeIntervalSince(date)) < abs($1.day.middayDate().timeIntervalSince(date)) }
        }
        let shown = pressed ?? points.last
        return VStack(alignment: .leading, spacing: 2) {
            Text(shown.map { Formatting.date($0.day, today: today) } ?? "")
                .font(.subheadline)
                .foregroundStyle(Color(.secondaryLabel))
            HStack(alignment: .firstTextBaseline, spacing: 8) {
                Text(shown.map { value($0.value, measure: measure, unit: unit) } ?? "")
                    .font(.title.weight(.semibold).monospacedDigit())
                if let shown {
                    Text("\(Formatting.weight(shown.weight, unit)) × \(shown.reps)")
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
        }
        .accessibilityElement(children: .combine)
    }

    private func chart(_ points: [MovementPoint], measure: MovementMeasure, unit: WeightUnit) -> some View {
        Chart {
            ForEach(points) { point in
                LineMark(x: .value("Day", point.day.middayDate()), y: .value(measure.label, point.value))
                    .lineStyle(StrokeStyle(lineWidth: 1.5))
                    .foregroundStyle(Tokens.accent)
                PointMark(x: .value("Day", point.day.middayDate()), y: .value(measure.label, point.value))
                    .symbolSize(span.isLong ? 12 : 22)
                    .foregroundStyle(Tokens.accent)
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
                        Text(date, format: span.isLong ? .dateTime.month(.abbreviated).year() : .dateTime.month(.abbreviated).day())
                    }
                }
            }
        }
        .frame(height: chartHeight)
        .padding(.vertical, 6)
        .accessibilityLabel("\(measure.label) by session")
        .accessibilityValue(spoken(points, measure: measure, unit: unit))
    }

    /// Latest, best and the change over the span.
    private func summary(_ points: [MovementPoint], measure: MovementMeasure, unit: WeightUnit) -> some View {
        let latest = points.last
        let best = points.max { $0.value < $1.value }
        let change = (points.last?.value ?? 0) - (points.first?.value ?? 0)
        return SummaryFigures(figures: [
            ("Latest", latest.map { value($0.value, measure: measure, unit: unit) } ?? "—"),
            ("Best", best.map { value($0.value, measure: measure, unit: unit) } ?? "—"),
            ("Change", "\(change < 0 ? "−" : "+")\(value(abs(change), measure: measure, unit: unit))"),
        ])
    }

    private func value(_ value: Double, measure: MovementMeasure, unit: WeightUnit) -> String {
        switch measure {
        case .estimatedOneRepMax, .heaviestSet: Formatting.weight(value.rounded(toPlaces: 1), unit)
        case .bestSetVolume: Formatting.volume(value, unit)
        case .repsAt: Formatting.count(Int(value.rounded()), "rep")
        }
    }

    private func spoken(_ points: [MovementPoint], measure: MovementMeasure, unit: WeightUnit) -> String {
        guard let first = points.first, let last = points.last else { return "" }
        return "From \(value(first.value, measure: measure, unit: unit)) to \(value(last.value, measure: measure, unit: unit)) over \(Formatting.count(points.count, "session"))."
    }

    // MARK: - Frequency

    @ViewBuilder
    private var frequencySection: some View {
        let frequency = MovementFrequency.of(movement, today: today)
        let reachWeeks = span.weeks(of: frequency.weeks.count)
        let reach = Array(frequency.weeks.suffix(reachWeeks + TrainingTrends.loadAverageWeeks - 1))
        let shown = Array(reach.suffix(reachWeeks))
        let sessions = shown.reduce(0) { $0 + $1.sessionCount }

        if !shown.isEmpty {
            Section {
                SummaryFigures(figures: [
                    ("Sessions", "\(sessions)"),
                    ("Per week", Formatting.rate(MovementFrequency.rate(of: shown))),
                    ("Weeks with one", "\(shown.filter { $0.sessionCount > 0 }.count) of \(shown.count)"),
                ])
                WeeklyLoadChart(
                    weeks: shown,
                    average: TrainingTrends.trailingAverage(of: reach, counting: \.sessionCount),
                    counting: .sessions)
                    .frame(height: frequencyHeight)
                    .padding(.vertical, 6)
            } header: {
                Text("Sessions per week")
            } footer: {
                Text("Weeks with no session of this movement are counted.")
            }
        }
    }
}

private extension Double {
    func rounded(toPlaces places: Int) -> Double {
        let scale = pow(10, Double(places))
        return (self * scale).rounded() / scale
    }
}
