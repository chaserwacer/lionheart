import SwiftUI

/// Bodyweight: log today's, see the trend.
///
/// One reading a day, in pounds by default because that is the unit the athlete lifts in, and the
/// number that turns a weighted dip's plate into the load he moved. The trend draws every reading
/// over a month, a quarter or a year; the readout is the latest, or the day pressed.
struct BodyweightView: View {
    let store: BodyweightStore

    @State private var span: TrendSpan = .quarter
    @State private var selection: Date?
    @State private var entry = ""
    @State private var unit: WeightUnit = .pounds
    @State private var day = Date()
    @FocusState private var isEntryFocused: Bool

    var body: some View {
        List {
            logSection
            switch store.state {
            case .loading:
                Section { SkeletonRows(count: 3) }
            case .unavailable(let failure):
                Section {
                    Text(failure.message).font(.footnote).foregroundStyle(Color(.secondaryLabel))
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await store.refresh() } }.font(.footnote)
                    }
                }
            case .loaded(let series):
                content(series, staleBecause: nil)
            case .stale(let series, let staleness):
                content(series, staleBecause: staleness)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Bodyweight")
        .navigationBarTitleDisplayMode(.inline)
        .refreshable { await store.refresh() }
        .task { await store.refreshIfNeeded() }
        .onChange(of: store.series.latest?.unit) { _, latest in
            if let latest, latest != unit, entry.isEmpty { unit = latest }
        }
        .alert(
            "Not logged",
            isPresented: Binding(
                get: { store.action.refusal != nil },
                set: { if !$0 { store.dismissRefusal() } })
        ) {
            Button("OK") { store.dismissRefusal() }
        } message: {
            if let refusal = store.action.refusal { Text(refusal.message) }
        }
    }

    // MARK: - Logging

    private var logSection: some View {
        Section {
            HStack(spacing: Tokens.cardPadding) {
                TextField("Weight", text: $entry, prompt: Text(store.series.latest.map { Formatting.measure($0.weight) } ?? "175"))
                    .keyboardType(.decimalPad)
                    .monospacedDigit()
                    .focused($isEntryFocused)
                    .accessibilityLabel("Bodyweight")
                Picker("Unit", selection: $unit) {
                    Text("lb").tag(WeightUnit.pounds)
                    Text("kg").tag(WeightUnit.kilograms)
                }
                .pickerStyle(.segmented)
                .fixedSize()
                Button("Log") { Task { await log() } }
                    .buttonStyle(.borderedProminent)
                    .disabled(loggable == nil || store.action.isRunning)
            }
            // The separator runs from the row's edge, as it does under the date, rather than from
            // wherever the trailing button happens to start.
            .alignmentGuide(.listRowSeparatorLeading) { _ in 0 }
            DatePicker("Day", selection: $day, in: ...Date(), displayedComponents: .date)
        } header: {
            Text("Log")
        } footer: {
            Text("One reading a day; logging a day again replaces it.")
        }
    }

    private var loggable: Double? {
        SetDraft.number(entry).flatMap { $0 > 0 && $0 <= 1000 ? $0 : nil }
    }

    private func log() async {
        guard let weight = loggable else { return }
        if await store.log(weight, unit, on: CalendarDay(day)) {
            entry = ""
            isEntryFocused = false
            Haptics.setLogged()
        }
    }

    // MARK: - The series

    @ViewBuilder
    private func content(_ series: BodyweightSeries, staleBecause staleness: Staleness?) -> some View {
        if let staleness { StaleBanner(staleness) }

        if let latest = series.latest {
            let from = CalendarDay(Date()).adding(days: 1 - span.days)
            let points = series.points(in: latest.unit, from: from)

            Section {
                Picker("Span", selection: $span) {
                    ForEach(TrendSpan.allCases) { Text($0.label).tag($0) }
                }
                .pickerStyle(.segmented)
                .listRowBackground(Color.clear)
                .listRowInsets(EdgeInsets())
            }

            Section {
                readout(series, latest: latest, points: points)
                if points.count >= 2 {
                    DailyTrendChart(
                        points: points,
                        mean: points.map(\.value).reduce(0, +) / Double(points.count),
                        showsMonthsOnly: span == .year,
                        selection: $selection)
                        .accessibilityLabel("Bodyweight over \(span.label)")
                }
            } footer: {
                if points.count >= 2 {
                    Text("Points are readings; the dashed rule is the mean of the span.")
                }
            }

            Section("Readings") {
                ForEach(series.measurements.reversed().prefix(30)) { measurement in
                    LabeledContent(Formatting.date(measurement.day)) {
                        Text(Formatting.weight(measurement.weight, measurement.unit)).monospacedDigit()
                    }
                    .swipeActions(edge: .trailing) {
                        Button(role: .destructive) {
                            Task { await store.delete(on: measurement.day) }
                        } label: {
                            Label("Delete", systemImage: "trash")
                        }
                    }
                }
            }
        } else {
            Section {
                Text("No bodyweight logged. Weighted dips and pull-ups read their load as bodyweight plus the plate once one is.")
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
    }

    private func readout(_ series: BodyweightSeries, latest: BodyweightMeasurement, points: [MetricPoint]) -> some View {
        let selected = DailyTrendChart.nearest(to: selection, in: points)
        let value = selected?.value ?? latest.weight
        let caption = selected.map { Formatting.date($0.day) } ?? "Latest · \(Formatting.dayInline(latest.day))"
        let change = selected == nil ? series.change(over: 30) : nil

        return HStack(alignment: .firstTextBaseline) {
            VStack(alignment: .leading, spacing: 2) {
                Text(caption)
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
                HStack(alignment: .firstTextBaseline, spacing: 4) {
                    Text(Formatting.measure(value)).font(.title.weight(.semibold).monospacedDigit())
                    Text(latest.unit.abbreviation).font(.subheadline).foregroundStyle(Color(.secondaryLabel))
                }
            }
            Spacer()
            if let change {
                VStack(alignment: .trailing, spacing: 2) {
                    Text("30 days").font(.subheadline).foregroundStyle(Color(.secondaryLabel))
                    Text(Formatting.signedWeight(change, latest.unit))
                        .font(.body.monospacedDigit())
                }
            }
        }
        .accessibilityElement(children: .combine)
    }
}
