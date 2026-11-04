import SwiftUI

/// A workout imported from Apple Health — a run, a ride, a hike, a walk: the route on a map, the
/// figures, the splits, the heart rate against the zones, and whatever else the recording app
/// measured — a ride's power and cadence, a run's form, the heart rate's recovery.
///
/// Every figure is the server's (`RecordedWorkout`). The page converts only the unit — kilometres or
/// miles, the athlete's choice, kept between workouts — and reads a ride as a speed.
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
            .map { "\($0.activity.title), \($0.start.formatted(.dateTime.weekday(.abbreviated).day().month(.abbreviated)))" }
            ?? "Workout"
    }

    @ViewBuilder
    private func content(_ run: RecordedWorkout) -> some View {
        routeSection(run)
        figuresSection(run)
        formSection(run)
        splitsSection(run)
        heartRateSection(run)
        zonesSection(run)
        tracesSection(run)
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
                Text(isIndoors(run) ? "Indoors: no route." : "No route was recorded with this \(noun(run)).")
                    .foregroundStyle(Color(.secondaryLabel))
            }
        } footer: {
            Text("Recorded by \(run.recordedBy.nonEmpty ?? "an app") and imported from Apple Health, \(run.start.formatted(date: .omitted, time: .shortened))–\(run.end.formatted(date: .omitted, time: .shortened)).\(run.route.count >= 2 ? " The route is kept on this phone and your server only; the chat model is given the figures, never the route." : "")")
        }
    }

    private func figuresSection(_ run: RecordedWorkout) -> some View {
        Section {
            FigureGrid(figures: figures(run))
        } footer: {
            Text(figuresFootnote(run))
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
        let rate = run.activity.readsAsSpeed ? "Speed" : "Pace"
        if let pace = run.pace {
            figures.append(OuraPanel.Figure(label: rate, value: self.rate(run, pace)))
        }
        if let movingPace = run.movingPace {
            figures.append(OuraPanel.Figure(label: "Moving \(rate.lowercased())", value: self.rate(run, movingPace)))
        }
        figures.append(OuraPanel.Figure(label: "Climb", value: "\(Int(run.climbMeters.rounded())) m"))
        if let descent = run.descentMeters {
            figures.append(OuraPanel.Figure(label: "Descent", value: "\(Int(descent.rounded())) m"))
        }
        if case .recorded(let average, let peak, _, _, _) = run.heartRate {
            figures.append(OuraPanel.Figure(label: "Avg HR", value: "\(average)"))
            figures.append(OuraPanel.Figure(label: "Peak HR", value: "\(peak)"))
        }
        if let recovery = run.metric(.heartRateRecovery) {
            figures.append(OuraPanel.Figure(label: "HR recovery", value: "−\(Int(recovery.average.rounded()))"))
        }
        // A ride's drivers lead its own figures; a run's power and form have a section of their own.
        if !run.activity.isRunning {
            for metric in [WorkoutMetric.power, .cadence] {
                guard let readout = run.metric(metric) else { continue }
                figures.append(OuraPanel.Figure(
                    label: "Avg \(WorkoutMetricText.name(metric, for: run.activity).lowercased())",
                    value: WorkoutMetricText.figure(metric, readout.average, for: run.activity, in: unit)))
            }
            if let power = run.metric(.power) {
                figures.append(OuraPanel.Figure(
                    label: "Peak power", value: WorkoutMetricText.figure(.power, power.peak, for: run.activity, in: unit)))
            }
        }
        return figures
    }

    private func rate(_ run: RecordedWorkout, _ perKilometre: TimeInterval) -> String {
        run.activity.readsAsSpeed ? unit.speed(perKilometre: perKilometre) : unit.pace(perKilometre: perKilometre)
    }

    private func figuresFootnote(_ run: RecordedWorkout) -> String {
        let app = run.recordedBy.nonEmpty ?? "the app"
        let rate = run.activity.readsAsSpeed ? "speed" : "pace"
        var lines = ["Time is what \(app) counted, its pauses left out."]
        if run.movingTime != nil {
            lines.append("Moving time is the route's time in motion, every stop removed, and moving \(rate) is per that time.")
        }
        if run.metric(.heartRateRecovery) != nil {
            lines.append("HR recovery is how far the heart rate fell in the first minute after the workout ended, as the watch measured it; a larger drop is a fitter heart.")
        }
        if !run.activity.isRunning, run.metric(.power) != nil {
            lines.append("Average power counts the coasting, as a power meter's own average does.")
        }
        return lines.joined(separator: " ")
    }

    /// A run's form, as the watch measured it: the averages, each explained.
    @ViewBuilder
    private func formSection(_ run: RecordedWorkout) -> some View {
        let form = WorkoutMetricText.runningForm(run)
        if !form.isEmpty {
            Section {
                FigureGrid(figures: form.map { readout in
                    OuraPanel.Figure(
                        label: WorkoutMetricText.name(readout.metric, for: run.activity),
                        value: WorkoutMetricText.figure(readout.metric, readout.average, for: run.activity, in: unit))
                })
            } header: {
                Text("Running form")
            } footer: {
                Text("Averages across the run, from \(run.recordedBy.nonEmpty ?? "the watch"). Ground contact is the time each foot is down; vertical oscillation is the bounce each stride; shorter contact and less bounce at the same pace are the more economical stride. Running power is the watch's estimate of the work, in watts.")
            }
        }
    }

    /// Each metric the app wrote often enough to draw, against its mean.
    @ViewBuilder
    private func tracesSection(_ run: RecordedWorkout) -> some View {
        ForEach(WorkoutMetricText.traced(run), id: \.metric) { readout in
            Section {
                MetricTraceChart(readout: readout, unit: unit, start: run.start, end: run.end)
                    .accessibilityLabel("\(WorkoutMetricText.name(readout.metric, for: run.activity)) across the \(noun(run))")
                    .accessibilityValue("Mean \(WorkoutMetricText.figure(readout.metric, readout.average, for: run.activity, in: unit)), peak \(WorkoutMetricText.figure(readout.metric, readout.peak, for: run.activity, in: unit))")
            } header: {
                Text("\(WorkoutMetricText.name(readout.metric, for: run.activity)), \(WorkoutMetricText.unitLabel(readout.metric, in: unit))")
            } footer: {
                Text("Mean \(WorkoutMetricText.figure(readout.metric, readout.average, for: run.activity, in: unit)), dashed; peak \(WorkoutMetricText.figure(readout.metric, readout.peak, for: run.activity, in: unit)).")
            }
        }
    }

    private func noun(_ run: RecordedWorkout) -> String {
        run.activity.title.lowercased()
    }

    private func isIndoors(_ run: RecordedWorkout) -> Bool {
        switch run.activity {
        case .endurance(.indoorRide), .endurance(.indoorRun): true
        case .run, .endurance: false
        }
    }

    /// The splits, under the unit the whole page reads in. Indoors there is no route to cut, so only
    /// the unit stays.
    @ViewBuilder
    private func splitsSection(_ run: RecordedWorkout) -> some View {
        let splits = unit.splits(of: run)
        if splits.isEmpty && isIndoors(run) {
            Section { unitPicker }
        } else {
            routeSplitsSection(run, splits: splits)
        }
    }

    private var unitPicker: some View {
        Picker("Unit", selection: $unit) {
            Text("Kilometres").tag(RunUnit.kilometres)
            Text("Miles").tag(RunUnit.miles)
        }
        .pickerStyle(.segmented)
        .listRowSeparator(.hidden)
    }

    private func routeSplitsSection(_ run: RecordedWorkout, splits: [WorkoutSplit]) -> some View {
        Section {
            unitPicker

            if splits.isEmpty {
                Text("No route to cut into splits.").foregroundStyle(Color(.secondaryLabel))
            } else {
                SplitsGrid(splits: splits, unit: unit, readsAsSpeed: run.activity.readsAsSpeed)
            }
        } header: {
            Text("Splits")
        } footer: {
            Text("\(run.activity.readsAsSpeed ? "Speed is per moving time" : "Pace is moving time per \(unit == .kilometres ? "kilometre" : "mile")"); a short last split reads at the \(run.activity.readsAsSpeed ? "speed" : "pace") it was covered at. Climb and descent are read from the route's altitudes, counting a change once it passes 3 m.")
        }
    }

    @ViewBuilder
    private func heartRateSection(_ run: RecordedWorkout) -> some View {
        Section {
            if run.trace.samples.count >= 2 {
                HeartRateZoneChart(
                    samples: run.trace.samples, maxHeartRate: run.maxHeartRate, start: run.start, end: run.end)
                    .accessibilityLabel("Heart rate across the \(noun(run))")
            } else {
                Text("No heart rate for this \(noun(run)): \(run.recordedBy.nonEmpty ?? "the app") wrote none to Health, and the ring's series has none over the same time.")
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
                Text("The heart rate covers \(coverage.formatted(.percent.precision(.fractionLength(0)))) of the \(noun(run)). Load \(zones.load.formatted(.number.precision(.fractionLength(0)))), Edwards: minutes times zone.")
            }
        }
    }
}

/// A workout's splits as a table: each one's distance where it is short, its pace — a ride's
/// speed — its climb and descent, and its average heart rate.
private struct SplitsGrid: View {
    let splits: [WorkoutSplit]
    let unit: RunUnit
    let readsAsSpeed: Bool

    var body: some View {
        let hasClimb = splits.contains { $0.climbMeters != nil }
        let hasHeartRate = splits.contains { $0.averageBpm != nil }

        Grid(alignment: .trailing, horizontalSpacing: 14, verticalSpacing: 6) {
            GridRow {
                Text(unit.abbreviation).gridColumnAlignment(.leading)
                Text(readsAsSpeed ? unit.speedAbbreviation : "Pace")
                if hasClimb { Text("+m"); Text("−m") }
                if hasHeartRate { Text("bpm") }
            }
            .font(.caption)
            .foregroundStyle(Color(.secondaryLabel))

            ForEach(splits) { split in
                GridRow {
                    Text(label(split)).gridColumnAlignment(.leading)
                    Text(readsAsSpeed ? unit.splitSpeed(split) : RunUnit.splitPace(split))
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
