import SwiftUI

/// One cardio session: its heart rate across the session against the zone bands, and the minutes
/// in each zone.
///
/// The curve is the all-day series, read a little either side of the session so the climb into it
/// and the recovery out of it are visible; dashed rules mark where the session begins and ends. A
/// run imported from Apple Health opens onto its own page instead (`RecordedWorkoutView`), which draws the
/// recording app's heart rate where it wrote one.
struct CardioSessionView: View {
    let session: CardioSession
    let maxHeartRate: MaxHeartRate?
    let store: CardioStore
    /// Why the cardio report left this workout out, when it did; `nil` for a session it counted.
    var declinedBecause: ExcludedWorkouts.Reason?

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
        if let declinedBecause {
            return "Left out of the cardio figures and the score: \(CardioWords.verdict(declinedBecause)). \(CardioWords.evidenceRule)"
        }
        return switch session.source {
        case .loggedSession(_, _, isRecorded: true, _): "Imported from Apple Health. The curve is the ring's series; the workout's own page has the recording app's."
        case .loggedSession: "Logged in Lionheart; heart rate from the ring over the same time."
        case .ringWorkout(_, _, _, .detected): "Detected by the ring and counted because its heart rate showed training."
        case .ringWorkout(_, _, _, .entered): "Entered on the ring."
        case .ringWorkout(_, _, _, .unrecognised): "Recorded on the ring."
        case .unrecognised: ""
        }
    }

    private var figures: [OuraPanel.Figure] {
        var figures = [OuraPanel.Figure(label: "Duration", value: Formatting.minutes(session.minutes))]
        if let meters = session.distanceMeters, meters >= 1 {
            figures.append(OuraPanel.Figure(label: "Distance", value: OuraWorkout.distance(meters: meters)))
        }
        if case .recorded(let average, let peak, let coverage, let zones, _) = session.heartRate {
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
                    HeartRateZoneChart(samples: samples, maxHeartRate: maxHeartRate, start: session.start, end: session.end)
                        .accessibilityLabel("Heart rate across the session")
                        .accessibilityValue(figures.map { "\($0.label) \($0.value)" }.joined(separator: ", "))
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

    // MARK: - Zones

    @ViewBuilder
    private var zonesSection: some View {
        if case .recorded(_, _, _, let zones?, _) = session.heartRate {
            Section("Minutes in each zone") {
                ZoneMinutesGrid(zones: zones, maxHeartRate: maxHeartRate)
            }
        }
    }
}
