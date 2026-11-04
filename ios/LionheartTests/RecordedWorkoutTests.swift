import Foundation
import Testing

@testable import Lionheart

/// How a recorded workout's page reads its figures: a ride as a speed, a run's speed as a pace, the
/// metrics it traces for each activity, and a trainer's per-second series thinned for drawing.
/// Invented values throughout.
@Suite("Recorded workout page")
struct RecordedWorkoutTests {
    private static let start = Date(timeIntervalSince1970: 1_790_000_000)

    @Test("A pace per kilometre reads as a speed in either unit")
    func speedFromPace() {
        #expect(RunUnit.kilometres.speed(perKilometre: 120) == "30.0 km/h")
        #expect(RunUnit.miles.speed(perKilometre: 120) == "18.6 mph")
        #expect(RunUnit.kilometres.speed(metersPerSecond: 10) == "36.0 km/h")
    }

    @Test("A run's speed reads as a pace, a ride's as a speed")
    func speedByActivity() {
        #expect(WorkoutMetricText.figure(.speed, 1000.0 / 300, for: .run, in: .kilometres) == "5:00 /km")
        #expect(WorkoutMetricText.figure(.speed, 10, for: .endurance(.ride), in: .kilometres) == "36.0 km/h")
        #expect(WorkoutMetricText.figure(.verticalOscillation, 8.64, for: .run, in: .miles) == "8.6 cm")
    }

    @Test("A ride traces its power, cadence and speed; a run its power and form; neither a single reading")
    func tracedByActivity() {
        let ride = Self.workout(.endurance(.ride), metrics: [
            Self.readout(.speed), Self.readout(.cadence), Self.readout(.power), Self.readout(.heartRateRecovery, count: 1),
        ])
        let run = Self.workout(.run, metrics: [
            Self.readout(.strideLength), Self.readout(.groundContactTime), Self.readout(.cadence, count: 1),
        ])

        #expect(WorkoutMetricText.traced(ride).map(\.metric) == [.power, .cadence, .speed])
        #expect(WorkoutMetricText.traced(run).map(\.metric) == [.groundContactTime, .strideLength])
        #expect(WorkoutMetricText.runningForm(ride).isEmpty)
        #expect(WorkoutMetricText.runningForm(run).map(\.metric) == [.groundContactTime, .strideLength])
    }

    @Test("A long series is drawn as equal runs averaged; a short one as it is")
    func thinnedForDrawing() {
        let samples = (0..<10).map { MetricSample(at: Self.start.addingTimeInterval(Double($0)), value: Double($0)) }

        let drawn = MetricTrace.drawn(samples, limit: 4)

        #expect(drawn.map(\.value) == [1, 4, 7, 9])
        #expect(drawn.first?.at == Self.start)
        #expect(MetricTrace.drawn(samples, limit: 10) == samples)
    }

    @Test("Sets naming no movement are titled by their one activity, and anything mixed is other work")
    func unattachedTitles() {
        let common = SetEntry.Common(source: .formed, facets: [])
        let ride = SetEntry.endurance(.init(
            common: common, activity: .ride, distance: 40, unit: .kilometers, duration: 4800, elevationGainMeters: 200, rpe: .unrated))
        let run = SetEntry.trailRun(.init(
            common: common, distance: 8, unit: .kilometers, duration: 2400, elevationGainMeters: 80, rpe: .unrated))

        #expect(SessionDetail.unattachedTitle([ride]) == "Ride")
        #expect(SessionDetail.unattachedTitle([run]) == "Trail run")
        #expect(SessionDetail.unattachedTitle([ride, run]) == "Other work")
        #expect(SessionDetail.unattachedTitle([.freeform(common)]) == "Other work")
    }

    // MARK: - Fixtures

    private static func readout(_ metric: WorkoutMetric, count: Int = 3) -> MetricReadout {
        MetricReadout(
            metric: metric, average: 10, peak: 12,
            samples: (0..<count).map { MetricSample(at: start.addingTimeInterval(Double($0)), value: 10) })
    }

    private static func workout(_ activity: RecordedActivity, metrics: [MetricReadout]) -> RecordedWorkout {
        RecordedWorkout(
            trainingSessionId: UUID(), activity: activity, recordedBy: "An app", start: start, end: start.addingTimeInterval(3600),
            distanceMeters: 10_000, duration: 3600, movingTime: nil, pace: 360, movingPace: nil, climbMeters: 0,
            descentMeters: nil, heartRate: .notRecorded, maxHeartRate: nil,
            trace: HeartRateTrace(source: .ring, samples: []), kilometres: [], miles: [], route: [], metrics: metrics)
    }
}
