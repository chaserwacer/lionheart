import SwiftUI

/// A run imported from Apple Health: the route on a map, the figures, the splits, and the heart
/// rate against the zones.
///
/// Every figure is the server's (`RecordedWorkout`). The page converts only the unit — kilometres or
/// miles, the athlete's choice, kept between runs.
struct RecordedWorkoutView: View {
    let trainingSessionId: UUID
    let store: RecordedWorkoutStore

    @AppStorage("lh.runUnit") private var unit: RunUnit = .kilometres
    @State private var isMapExpanded = false

    /// A picture, not text, so it does not grow with Dynamic Type: at the largest sizes a scaled map
    /// would be taller than the screen.
    private let mapHeight: CGFloat = 260

    var body: some View {
        List {
            switch store.runs[trainingSessionId] ?? .loading {
            case .loading:
                Section { SkeletonRows(count: 4) }
            case .unavailable(let failure):
                Section {
                    Text(failure.message).foregroundStyle(Color(.secondaryLabel))
                    if failure.isWorthRetrying {
                        Button("Try again") { Task { await store.load(trainingSessionId) } }
                    }
                }
            case .loaded(let run):
                content(run)
            case .stale(let run, let staleness):
                StaleBanner(staleness)
                content(run)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle(title)
        .navigationBarTitleDisplayMode(.inline)
        .refreshable { await store.load(trainingSessionId) }
        .task(id: trainingSessionId) {
            if store.runs[trainingSessionId]?.value == nil { await store.load(trainingSessionId) }
        }
    }

    private var title: String {
        store.runs[trainingSessionId]?.value
            .map { $0.start.formatted(.dateTime.weekday(.abbreviated).day().month(.abbreviated)) } ?? "Run"
    }

    @ViewBuilder
    private func content(_ run: RecordedWorkout) -> some View {
        routeSection(run)
        figuresSection(run)
        splitsSection(run)
        heartRateSection(run)
        zonesSection(run)
    }

    // MARK: - Sections

    @ViewBuilder
    private func routeSection(_ run: RecordedWorkout) -> some View {
        Section {
            if run.route.count >= 2 {
                Button { isMapExpanded = true } label: {
                    RouteMap(route: run.route)
                        .frame(height: mapHeight)
                        .allowsHitTesting(false)
                }
                .buttonStyle(.plain)
                .accessibilityHint("Opens the route full screen.")
                .listRowInsets(EdgeInsets())
                .fullScreenCover(isPresented: $isMapExpanded) {
                    RouteMapSheet(route: run.route)
                }
            } else {
                Text("No route was recorded with this run.").foregroundStyle(Color(.secondaryLabel))
            }
        } footer: {
            Text("Recorded by \(run.recordedBy.nonEmpty ?? "an app") and imported from Apple Health, \(run.start.formatted(date: .omitted, time: .shortened))–\(run.end.formatted(date: .omitted, time: .shortened)). The route is kept on this phone and your server only; the chat model is given the figures, never the route.")
        }
    }

    private func figuresSection(_ run: RecordedWorkout) -> some View {
        Section {
            FigureGrid(figures: figures(run))
        } footer: {
            Text("Time is what \(run.recordedBy.nonEmpty ?? "the app") counted, its pauses left out. Moving time is the route's time in motion, read from it with every stop removed, and moving pace is per that time.")
        }
    }

    private func figures(_ run: RecordedWorkout) -> [OuraPanel.Figure] {
        var figures = [
            OuraPanel.Figure(label: "Distance", value: unit.distance(run.distanceMeters)),
            OuraPanel.Figure(label: "Time", value: MeasurementText.clock(seconds: run.duration)),
        ]
        if let moving = run.movingTime {
            figures.append(OuraPanel.Figure(label: "Moving", value: MeasurementText.clock(seconds: moving)))
        }
        if let pace = run.pace {
            figures.append(OuraPanel.Figure(label: "Pace", value: unit.pace(perKilometre: pace)))
        }
        if let movingPace = run.movingPace {
            figures.append(OuraPanel.Figure(label: "Moving pace", value: unit.pace(perKilometre: movingPace)))
        }
        figures.append(OuraPanel.Figure(label: "Climb", value: "\(Int(run.climbMeters.rounded())) m"))
        if let descent = run.descentMeters {
            figures.append(OuraPanel.Figure(label: "Descent", value: "\(Int(descent.rounded())) m"))
        }
        if case .recorded(let average, let peak, _, _, _) = run.heartRate {
            figures.append(OuraPanel.Figure(label: "Avg HR", value: "\(average)"))
            figures.append(OuraPanel.Figure(label: "Peak HR", value: "\(peak)"))
        }
        return figures
    }

    @ViewBuilder
    private func splitsSection(_ run: RecordedWorkout) -> some View {
        let splits = unit.splits(of: run)
        Section {
            Picker("Unit", selection: $unit) {
                Text("Kilometres").tag(RunUnit.kilometres)
                Text("Miles").tag(RunUnit.miles)
            }
            .pickerStyle(.segmented)
            .listRowSeparator(.hidden)

            if splits.isEmpty {
                Text("No route to cut into splits.").foregroundStyle(Color(.secondaryLabel))
            } else {
                SplitsGrid(splits: splits, unit: unit)
            }
        } header: {
            Text("Splits")
        } footer: {
            Text("Pace is moving time per \(unit == .kilometres ? "kilometre" : "mile"); a short last split reads at the pace it was run. Climb and descent are read from the route's altitudes, counting a change once it passes 3 m.")
        }
    }

    @ViewBuilder
    private func heartRateSection(_ run: RecordedWorkout) -> some View {
        Section {
            if run.trace.samples.count >= 2 {
                HeartRateZoneChart(
                    samples: run.trace.samples, maxHeartRate: run.maxHeartRate, start: run.start, end: run.end)
                    .accessibilityLabel("Heart rate across the run")
            } else {
                Text("No heart rate for this run: \(run.recordedBy.nonEmpty ?? "the app") wrote none to Health, and the ring's series has none over the same time.")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        } header: {
            Text("Heart rate, bpm")
        } footer: {
            if run.trace.samples.count >= 2 {
                Text(heartRateSource(run))
            }
        }
    }

    private func heartRateSource(_ run: RecordedWorkout) -> String {
        switch run.trace.source {
        case .appleHealth:
            "From \(run.recordedBy.nonEmpty ?? "the recording app") via Apple Health. Bands are the five zones."
        case .ring:
            "From the Oura ring's all-day series over the same time: \(run.recordedBy.nonEmpty ?? "the recording app") wrote no heart rate to Health. Bands are the five zones."
        case .unrecognised:
            "Bands are the five zones."
        }
    }

    @ViewBuilder
    private func zonesSection(_ run: RecordedWorkout) -> some View {
        if case .recorded(_, _, let coverage, let zones?, _) = run.heartRate {
            Section {
                ZoneMinutesGrid(zones: zones, maxHeartRate: run.maxHeartRate)
            } header: {
                Text("Minutes in each zone")
            } footer: {
                Text("The heart rate covers \(coverage.formatted(.percent.precision(.fractionLength(0)))) of the run. Load \(zones.load.formatted(.number.precision(.fractionLength(0)))), Edwards: minutes times zone.")
            }
        }
    }
}

/// A run's splits as a table: each one's distance where it is short, its pace, its climb and
/// descent, and its average heart rate.
private struct SplitsGrid: View {
    let splits: [WorkoutSplit]
    let unit: RunUnit

    var body: some View {
        let hasClimb = splits.contains { $0.climbMeters != nil }
        let hasHeartRate = splits.contains { $0.averageBpm != nil }

        Grid(alignment: .trailing, horizontalSpacing: 14, verticalSpacing: 6) {
            GridRow {
                Text(unit.abbreviation).gridColumnAlignment(.leading)
                Text("Pace")
                if hasClimb { Text("+m"); Text("−m") }
                if hasHeartRate { Text("bpm") }
            }
            .font(.caption)
            .foregroundStyle(Color(.secondaryLabel))

            ForEach(splits) { split in
                GridRow {
                    Text(label(split)).gridColumnAlignment(.leading)
                    Text(RunUnit.splitPace(split))
                    if hasClimb {
                        Text(split.climbMeters.map { "\(Int($0.rounded()))" } ?? "")
                        Text(split.descentMeters.map { "\(Int($0.rounded()))" } ?? "")
                    }
                    if hasHeartRate {
                        Text(split.averageBpm.map(String.init) ?? "")
                    }
                }
                .font(.subheadline.monospacedDigit())
                .accessibilityElement(children: .combine)
            }
        }
        .padding(.vertical, 2)
    }

    /// The split's number, or for a short last one, the distance it covered: "0.42".
    private func label(_ split: WorkoutSplit) -> String {
        split.distanceMeters < unit.meters - 0.5
            ? String(format: "%.2f", split.distanceMeters / unit.meters)
            : "\(split.number)"
    }
}
