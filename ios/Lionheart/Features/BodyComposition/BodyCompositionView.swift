import Charts
import SwiftUI

/// Body composition: the latest scan as an instrument, the trends across scans, and every scan.
///
/// With one scan there is nothing to draw a line through, and the trend section says so rather than
/// drawing a lone point on an axis that implies a series.
struct BodyCompositionView: View {
    let store: BodyCompositionStore
    let bodyweight: BodyweightStore

    @State private var isAdding = false
    @State private var correcting: BodyCompositionScan?
    @State private var measure: CompositionMeasure = .skeletalMuscleMass
    @State private var selection: Date?

    var body: some View {
        List {
            switch store.state {
            case .loading:
                Section { SkeletonRows(count: 4) }
            case .unavailable(let failure):
                Section {
                    Text(failure.message).font(.footnote).foregroundStyle(Color(.secondaryLabel))
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await store.refresh() } }.font(.footnote)
                    }
                }
            case .loaded(let history):
                content(history, staleBecause: nil)
            case .stale(let history, let staleness):
                content(history, staleBecause: staleness)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Body composition")
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button("Add scan") { isAdding = true }
            }
        }
        .refreshable {
            async let scans: Void = store.refresh()
            async let weighed: Void = bodyweight.refresh()
            _ = await (scans, weighed)
        }
        .task {
            async let scans: Void = store.refreshIfNeeded()
            async let weighed: Void = bodyweight.refreshIfNeeded()
            _ = await (scans, weighed)
        }
        .sheet(isPresented: $isAdding) {
            ScanEntrySheet(store: store, bodyweight: bodyweight)
        }
        .sheet(item: $correcting) { scan in
            ScanEntrySheet(store: store, bodyweight: bodyweight, correcting: scan)
        }
        .alert(
            "Not deleted",
            isPresented: Binding(get: { store.action.refusal != nil }, set: { if !$0 { store.dismissRefusal() } })
        ) {
            Button("OK") { store.dismissRefusal() }
        } message: {
            if let refusal = store.action.refusal { Text(refusal.message) }
        }
    }

    @ViewBuilder
    private func content(_ history: CompositionHistory, staleBecause staleness: Staleness?) -> some View {
        if let staleness { StaleBanner(staleness) }

        if let latest = history.latest {
            ScanReadout(scan: latest, history: history)
            trends(history)
            ScanPanels(sheet: latest.sheet)
            bodyweightRow
            scans(history)
        } else {
            Section {
                ContentUnavailableView {
                    Text("No scans")
                } description: {
                    Text("Add a body-composition sheet by photographing it, choosing a photo or PDF of it, or typing its figures.")
                } actions: {
                    Button("Add scan") { isAdding = true }
                }
                .listRowBackground(Color.clear)
            }
            bodyweightRow
        }
    }

    // MARK: - Trends

    @ViewBuilder
    private func trends(_ history: CompositionHistory) -> some View {
        let points = history.points(measure)
        Section {
            Picker("Measure", selection: $measure) {
                ForEach(CompositionMeasure.allCases, id: \.self) { Text($0.title).tag($0) }
            }
            if points.count >= 2 {
                readout(points, history: history)
                DailyTrendChart(points: points, showsMonthsOnly: spanIsLong(points), selection: $selection)
                    .accessibilityLabel("\(measure.title) across scans")
            } else if let only = points.first {
                Text("One scan: \(value(only.value, history: history)) on \(Formatting.date(only.day)). A trend starts with the second.")
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
            } else {
                Text("No scan carries this figure.")
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        } header: {
            Text("Across scans")
        }

        let balances = history.balances
        if balances.count >= 2 {
            Section {
                BalanceTrendChart(balances: balances)
            } header: {
                Text("Lean pattern across scans")
            } footer: {
                Text("Percent of the analyser's reference: arms and legs are the mean of left and right.")
            }
        }
    }

    private func readout(_ points: [MetricPoint], history: CompositionHistory) -> some View {
        let selected = DailyTrendChart.nearest(to: selection, in: points)
        let shown = selected ?? points[points.count - 1]
        return LabeledContent {
            Text(value(shown.value, history: history)).font(.title3.weight(.semibold).monospacedDigit())
        } label: {
            Text(selected == nil ? "Latest · \(Formatting.date(shown.day))" : Formatting.date(shown.day))
                .foregroundStyle(Color(.secondaryLabel))
        }
    }

    private func value(_ value: Double, history: CompositionHistory) -> String {
        switch measure {
        case .percentBodyFat: SheetFigure.percent(value)
        case .extracellularWaterRatio: String(format: "%.3f", value)
        case .weight, .skeletalMuscleMass, .bodyFatMass, .leanBodyMass:
            SheetFigure.mass(value, .pounds)
        }
    }

    private func spanIsLong(_ points: [MetricPoint]) -> Bool {
        guard let first = points.first, let last = points.last else { return false }
        return first.day.days(until: last.day) > 180
    }

    // MARK: - Bodyweight and the scans

    private var bodyweightRow: some View {
        Section {
            NavigationLink(value: BodyRoute.bodyweight) {
                LabeledContent("Bodyweight") {
                    if let latest = bodyweight.series.latest {
                        Text("\(Formatting.weight(latest.weight, latest.unit)) · \(Formatting.date(latest.day))")
                            .monospacedDigit()
                    } else {
                        Text("None logged")
                    }
                }
            }
        } footer: {
            Text("A scan logs its weight as that day's bodyweight, which weighted dips read as part of the load.")
        }
    }

    private func scans(_ history: CompositionHistory) -> some View {
        Section("Scans") {
            ForEach(history.scans.reversed()) { scan in
                NavigationLink(value: BodyRoute.scan(scan.id)) {
                    ScanRow(scan: scan)
                }
                .swipeActions(edge: .trailing) {
                    Button(role: .destructive) {
                        Task { await store.delete(scan) }
                    } label: {
                        Label("Delete", systemImage: "trash")
                    }
                }
                .contextMenu {
                    if scan.sheet.isWhollyUnderstood {
                        Button("Correct") { correcting = scan }
                    }
                    Button("Delete", role: .destructive) { Task { await store.delete(scan) } }
                }
            }
        }
    }
}

/// One scan in the list: its day and analyser, and the figures it is followed by.
struct ScanRow: View {
    let scan: BodyCompositionScan

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text("\(Formatting.date(scan.day)) · \(scan.sheet.device)")
            Text("\(SheetFigure.mass(scan.sheet.weight, scan.sheet.unit)) · muscle \(SheetFigure.number(Imperial.pounds(scan.sheet.skeletalMuscleMass, from: scan.sheet.unit))) · fat \(SheetFigure.percent(scan.sheet.percentBodyFat))")
                .font(.subheadline.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
        }
        .padding(.vertical, 1)
    }
}

/// The arm, trunk and leg pattern of lean mass across scans, each as the analyser's percentage of
/// reference.
private struct BalanceTrendChart: View {
    let balances: [(day: CalendarDay, balance: SegmentalBalance)]

    @ScaledMetric(relativeTo: .body) private var height: CGFloat = 180

    var body: some View {
        Chart {
            RuleMark(y: .value("Reference", 100))
                .lineStyle(StrokeStyle(lineWidth: 1, dash: [3, 3]))
                .foregroundStyle(Color(.tertiaryLabel))
            ForEach(Array(balances.enumerated()), id: \.offset) { _, entry in
                LineMark(x: .value("Day", entry.day.middayDate()), y: .value("Percent", entry.balance.armsOfReference))
                    .foregroundStyle(by: .value("Segment", "Arms"))
                LineMark(x: .value("Day", entry.day.middayDate()), y: .value("Percent", entry.balance.trunkOfReference))
                    .foregroundStyle(by: .value("Segment", "Trunk"))
                LineMark(x: .value("Day", entry.day.middayDate()), y: .value("Percent", entry.balance.legsOfReference))
                    .foregroundStyle(by: .value("Segment", "Legs"))
            }
        }
        .chartForegroundStyleScale(["Arms": Tokens.accent, "Trunk": Tokens.iron, "Legs": Tokens.ink])
        .chartYScale(domain: .automatic(includesZero: false))
        .frame(height: height)
        .padding(.vertical, 8)
        .accessibilityLabel("Arms, trunk and legs as percent of reference across scans")
    }
}
