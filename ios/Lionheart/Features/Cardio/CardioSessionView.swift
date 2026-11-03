import Charts
import SwiftUI

/// One cardio session: its heart rate across the session against the zone bands, and the minutes
/// in each zone.
///
/// The curve is the all-day series, read a little either side of the session so the climb into it
/// and the recovery out of it are visible; dashed rules mark where the session begins and ends.
struct CardioSessionView: View {
    let session: CardioSession
    let maxHeartRate: MaxHeartRate?
    let store: CardioStore

    @ScaledMetric(relativeTo: .body) private var chartHeight: CGFloat = 220

    var body: some View {
        List {
            Section {
                FigureGrid(figures: figures)
            } footer: {
                Text("\(session.start.formatted(date: .abbreviated, time: .shortened)) to \(session.end.formatted(date: .omitted, time: .shortened)). \(sourceText)")
            }

            curveSection
            zonesSection
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle(session.activityName)
        .navigationBarTitleDisplayMode(.inline)
        .refreshable { await store.loadHeartRate(for: session) }
        .task {
            if store.heartRate[session.id]?.value == nil { await store.loadHeartRate(for: session) }
        }
    }

    private var sourceText: String {
        switch session.source {
        case .loggedSession: "Logged in Lionheart; heart rate from the ring over the same time."
        case .ringWorkout(_, _, .walk): "A walk the ring detected, long enough to count as a session."
        case .ringWorkout: "Detected or entered on the ring."
        case .unrecognised: ""
        }
    }

    private var figures: [OuraPanel.Figure] {
        var figures = [OuraPanel.Figure(label: "Duration", value: Formatting.minutes(session.minutes))]
        if let meters = session.distanceMeters, meters >= 1 {
            figures.append(OuraPanel.Figure(label: "Distance", value: OuraWorkout.distance(meters: meters)))
        }
        if case .recorded(let average, let peak, let coverage, let zones) = session.heartRate {
            figures.append(OuraPanel.Figure(label: "Avg HR", value: "\(average)"))
            figures.append(OuraPanel.Figure(label: "Peak HR", value: "\(peak)"))
            figures.append(OuraPanel.Figure(
                label: "Covered", value: coverage.formatted(.percent.precision(.fractionLength(0)))))
            if let zones {
                figures.append(OuraPanel.Figure(label: "Load", value: zones.load.formatted(.number.precision(.fractionLength(0)))))
            }
        }
        return figures
    }

    // MARK: - The curve

    @ViewBuilder
    private var curveSection: some View {
        Section {
            switch store.heartRate[session.id] ?? .loading {
            case .loading:
                SkeletonRows(count: 2)
            case .unavailable(let failure):
                Text(failure.message).foregroundStyle(Color(.secondaryLabel))
            case .loaded(let samples), .stale(let samples, _):
                if samples.count >= 2 {
                    chart(samples)
                } else {
                    Text("No heart rate stored for this session. The series reaches back as far as it has been synced.")
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
        } header: {
            Text("Heart rate, bpm")
        } footer: {
            Text("Bands are the five zones; dashed rules, the session. Coverage below about half means the zone minutes describe part of the session, not all of it.")
        }
    }

    private func chart(_ samples: [HeartRateSample]) -> some View {
        let floors = maxHeartRate?.zoneFloors ?? []
        let top = Double(max(maxHeartRate?.bpm ?? 0, samples.map(\.bpm).max() ?? 0))
        let low = Double(samples.map(\.bpm).min() ?? 0)
        let from = samples.first?.at ?? session.start
        let to = samples.last?.at ?? session.end

        return Chart {
            ForEach(Array(floors.enumerated()), id: \.offset) { index, floor in
                RectangleMark(
                    xStart: .value("From", from), xEnd: .value("To", to),
                    yStart: .value("Floor", Double(floor)),
                    yEnd: .value("Ceiling", index + 1 < floors.count ? Double(floors[index + 1]) : top))
                    .foregroundStyle(ZoneColour.of(index + 1).opacity(0.22))
            }
            ForEach([session.start, session.end], id: \.self) { edge in
                RuleMark(x: .value("Session", edge))
                    .lineStyle(StrokeStyle(lineWidth: 1, dash: [3, 3]))
                    .foregroundStyle(Color(.secondaryLabel))
            }
            ForEach(Array(samples.enumerated()), id: \.offset) { _, sample in
                LineMark(x: .value("Time", sample.at), y: .value("bpm", sample.bpm))
                    .interpolationMethod(.monotone)
                    .lineStyle(StrokeStyle(lineWidth: 1.5))
                    .foregroundStyle(Color(.label))
            }
        }
        .chartYScale(domain: max(0, min(low, Double(floors.first ?? Int(low))) - 5)...(top + 5))
        .chartXAxis {
            AxisMarks(values: .automatic(desiredCount: 4)) { _ in
                AxisGridLine()
                AxisValueLabel(format: .dateTime.hour().minute())
            }
        }
        .frame(height: chartHeight)
        .padding(.vertical, 6)
        .accessibilityLabel("Heart rate across the session")
        .accessibilityValue(figures.map { "\($0.label) \($0.value)" }.joined(separator: ", "))
    }

    // MARK: - Zones

    @ViewBuilder
    private var zonesSection: some View {
        if case .recorded(_, _, _, let zones?) = session.heartRate {
            let floors = maxHeartRate?.zoneFloors ?? []
            Section("Minutes in each zone") {
                Grid(alignment: .leading, horizontalSpacing: 12, verticalSpacing: 6) {
                    ForEach(Array(zones.byZone.enumerated()).reversed(), id: \.offset) { index, minutes in
                        GridRow {
                            RoundedRectangle(cornerRadius: 2).fill(ZoneColour.of(index + 1)).frame(width: 10, height: 10)
                            Text("Zone \(index + 1)").font(.subheadline)
                            Text(floors.count == 5 ? "\(floors[index])+ bpm" : "")
                                .font(.caption.monospacedDigit())
                                .foregroundStyle(Color(.secondaryLabel))
                            Text(Formatting.minutes(minutes))
                                .font(.subheadline.monospacedDigit())
                                .gridColumnAlignment(.trailing)
                        }
                        .accessibilityElement(children: .combine)
                    }
                    GridRow {
                        Color.clear.frame(width: 10, height: 10)
                        Text("Below zone 1").font(.subheadline).foregroundStyle(Color(.secondaryLabel))
                        Text("")
                        Text(Formatting.minutes(zones.belowZones))
                            .font(.subheadline.monospacedDigit())
                            .foregroundStyle(Color(.secondaryLabel))
                    }
                }
                .padding(.vertical, 2)
            }
        }
    }
}
