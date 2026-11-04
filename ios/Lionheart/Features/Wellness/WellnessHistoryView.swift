import Charts
import SwiftUI

/// The athlete's wellness ratings over time, beside Oura's readiness for the same days.
///
/// The question it answers is whether his own sense of recovery agrees with the ring's: the two on
/// one time axis, then the mean readiness on the days he rated low, middling and high, and how
/// closely the two move together. The overall figure is the server's mean of the four ratings,
/// with stress counted the other way round, printed as received.
struct WellnessHistoryView: View {
    let store: WellnessStore

    @State private var span: TrendSpan = .year
    @State private var isLogging = false

    @ScaledMetric(relativeTo: .body) private var chartHeight: CGFloat = 110

    /// The spans offered. A month holds too few ratings to compare anything.
    private static let spans: [TrendSpan] = [.quarter, .year]

    var body: some View {
        List {
            Section {
                Picker("Span", selection: $span) {
                    ForEach(Self.spans) { Text($0.label).tag($0) }
                }
                .pickerStyle(.segmented)
                .listRowBackground(Color.clear)
                .listRowInsets(EdgeInsets())
            } footer: {
                Text(Formatting.spanCaption(span, endingOn: store.today))
            }

            switch store.spans[span] ?? .loading {
            case .loading:
                Section { SkeletonRows(count: 4) }
            case .unavailable(let failure):
                ContentUnavailableView {
                    Text("Wellness not available")
                } description: {
                    Text(failure.message)
                } actions: {
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await store.load(span) } }
                    }
                }
                .listRowBackground(Color.clear)
            case .loaded(let reading):
                content(reading, staleBecause: nil)
            case .stale(let reading, let staleness):
                content(reading, staleBecause: staleness)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Wellness")
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button("Log wellness", systemImage: "plus") { isLogging = true }
            }
        }
        .sheet(isPresented: $isLogging) {
            WellnessEntrySheet(
                store: store, existing: store.spans[span]?.value?.days.map(\.state) ?? [],
                since: store.today.adding(days: 1 - span.days))
        }
        .refreshable { await store.load(span) }
        .task(id: span) {
            if store.spans[span]?.value == nil { await store.load(span) }
        }
    }

    @ViewBuilder
    private func content(_ reading: WellnessAgainstReadiness, staleBecause staleness: Staleness?) -> some View {
        if let staleness { StaleBanner(staleness) }

        if reading.days.isEmpty {
            ContentUnavailableView {
                Text("No ratings in this span")
            } description: {
                Text("Rate a day with + here or on Body: energy, mood, motivation and stress, 1 to 5.")
            }
            .listRowBackground(Color.clear)
        } else {
            chartSection(reading)
            agreementSection(reading)
            entriesSection(reading)
        }
    }

    // MARK: - Over time

    private func chartSection(_ reading: WellnessAgainstReadiness) -> some View {
        let domain = store.today.adding(days: 1 - span.days).middayDate()...store.today.middayDate()
        return Section {
            VStack(alignment: .leading, spacing: 4) {
                Text("Readiness, Oura's score out of 100")
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
                Chart(reading.readiness) { point in
                    LineMark(x: .value("Day", point.day.middayDate()), y: .value("Readiness", point.value))
                        .interpolationMethod(.monotone)
                        .foregroundStyle(Color(.secondaryLabel))
                }
                .chartXScale(domain: domain)
                .chartYScale(domain: .automatic(includesZero: false))
                .chartXAxis(.hidden)
                .frame(height: chartHeight)
                .accessibilityLabel("Oura's readiness each day")
            }
            VStack(alignment: .leading, spacing: 4) {
                Text("Your overall rating, 1 to 5")
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
                Chart(reading.days) { day in
                    PointMark(
                        x: .value("Day", day.state.date.middayDate()),
                        y: .value("Overall", day.state.overallScore))
                        .foregroundStyle(Tokens.accent)
                        .symbolSize(span == .year ? 18 : 30)
                }
                .chartXScale(domain: domain)
                .chartYScale(domain: Double(WellnessReading.scale.lowerBound)...Double(WellnessReading.scale.upperBound))
                .chartXAxis {
                    AxisMarks(values: .automatic(desiredCount: 4)) { _ in
                        AxisGridLine()
                        AxisValueLabel(format: .dateTime.month(.abbreviated).day())
                    }
                }
                .frame(height: chartHeight)
                .accessibilityLabel("Your overall wellness rating on each day you rated")
            }
        } header: {
            Text("Over time")
        } footer: {
            Text("Overall is the mean of your four ratings, with stress counted the other way round: a calm day raises it.")
        }
    }

    // MARK: - Agreement

    @ViewBuilder
    private func agreementSection(_ reading: WellnessAgainstReadiness) -> some View {
        let paired = reading.paired.count
        Section {
            if paired == 0 {
                Text("No rated day has a readiness from the ring.").foregroundStyle(Color(.secondaryLabel))
            }
            ForEach(reading.bands) { band in
                LabeledContent {
                    Text(MeasurementText.plain(band.meanReadiness.rounded())).monospacedDigit()
                } label: {
                    VStack(alignment: .leading, spacing: 1) {
                        Text(band.label)
                        Text(Formatting.count(band.days, "day"))
                            .font(.caption.monospacedDigit())
                            .foregroundStyle(Color(.secondaryLabel))
                    }
                }
            }
            if let r = reading.correlation {
                LabeledContent {
                    Text(r.formatted(.number.precision(.fractionLength(2)))).monospacedDigit()
                } label: {
                    VStack(alignment: .leading, spacing: 1) {
                        Text("Correlation")
                        Text(WellnessAgainstReadiness.strength(of: r).capitalisedFirst)
                            .font(.caption)
                            .foregroundStyle(Color(.secondaryLabel))
                    }
                }
            }
        } header: {
            Text("Readiness on the days you rated")
        } footer: {
            Text(agreementFooter(paired: paired, hasCorrelation: reading.correlation != nil))
        }
    }

    private func agreementFooter(paired: Int, hasCorrelation: Bool) -> String {
        let bands = "Oura's mean readiness on the days your overall rating fell in each band, across \(Formatting.count(paired, "day")) with both."
        let r = hasCorrelation
            ? " Correlation runs from −1 to 1: near 0, the two do not move together; towards 1, a good day by your account is a good day by the ring's."
            : " A correlation needs at least \(WellnessAgainstReadiness.minimumPairs) days with both."
        return bands + r
    }

    // MARK: - Entries

    private func entriesSection(_ reading: WellnessAgainstReadiness) -> some View {
        Section("Ratings") {
            ForEach(reading.days.reversed()) { day in
                WellnessRow(reading: WellnessReading.of(day.state), readiness: day.readiness, today: store.today)
                    .contextMenu {
                        Button {
                            Clipboard.copy(Self.summary(of: WellnessReading.of(day.state)))
                        } label: {
                            Label("Copy", systemImage: "doc.on.doc")
                        }
                    }
            }
        }
    }

    /// One reading as a line of text: the day, the overall figure, and the four behind it.
    static func summary(of reading: WellnessReading) -> String {
        let components = reading.components.map { "\($0.name) \($0.score)" }
        return ([
            Formatting.date(reading.day),
            "overall \(MeasurementText.plain(reading.overall))",
        ] + components).joined(separator: " · ")
    }
}

/// One rated day: the overall figure, Oura's readiness beside it, and the four ratings.
private struct WellnessRow: View {
    let reading: WellnessReading
    let readiness: OuraScore?
    let today: CalendarDay

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .firstTextBaseline) {
                Text(Formatting.day(RelativeDay.of(reading.day, today: today)))
                    .font(.headline)
                Spacer()
                Text(MeasurementText.plain(reading.overall))
                    .font(.headline.monospacedDigit())
                if let readiness {
                    Text("Oura readiness \(readiness.description)")
                        .font(.caption.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
            // Top-aligned so the figures stay level when a label wraps at the larger Dynamic Type
            // sizes.
            HStack(alignment: .top, spacing: 12) {
                ForEach(reading.components) { component in
                    StatTile(value: "\(component.score)", label: component.name)
                }
            }
        }
        .padding(.vertical, 4)
        .accessibilityElement(children: .combine)
        .accessibilityLabel(
            "\(Formatting.date(reading.day)), overall \(MeasurementText.plain(reading.overall))\(readiness.map { ", Oura readiness \($0.description)" } ?? "")")
    }
}

private extension String {
    var capitalisedFirst: String { prefix(1).uppercased() + dropFirst() }
}
