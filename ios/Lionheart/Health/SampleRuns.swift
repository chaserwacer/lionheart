#if DEBUG
import CoreLocation
import HealthKit

/// Invented runs written to this device's Health store, so the import and the run page can be seen
/// working in a simulator, whose Health store starts empty.
///
/// **Debug builds only.** It is the one thing in the app that asks to write to Health, and it is
/// compiled out of Release, which therefore never asks. Every value is made up: the loops circle an
/// open meadow nobody here lives near, at paces and heart rates no one recorded.
enum SampleRuns {
    /// Writes the sample runs, each ending before `now`, asking for write access first.
    static func write(to store: HKHealthStore, now: Date = Date()) async throws {
        let writes: Set<HKSampleType> = [
            HKObjectType.workoutType(),
            HKSeriesType.workoutRoute(),
            HKQuantityType(.heartRate),
            HKQuantityType(.distanceWalkingRunning),
        ]
        try await store.requestAuthorization(toShare: writes, read: HealthLibrary.readTypes)

        for run in samples(before: now) {
            try await write(run, to: store)
        }
    }

    /// One invented run: a loop of a given length at an even pace over rolling ground.
    private struct Sample {
        let start: Date
        let meters: Double
        let metersPerSecond: Double
        let hills: Double
        let bpm: Int?

        var seconds: TimeInterval { meters / metersPerSecond }
        var end: Date { start.addingTimeInterval(seconds) }
    }

    private static func samples(before now: Date) -> [Sample] {
        let day: TimeInterval = 86_400
        let morning = Calendar.current.startOfDay(for: now).addingTimeInterval(7 * 3600)
        return [
            Sample(start: morning.addingTimeInterval(-7 * day), meters: 12_400, metersPerSecond: 2.9, hills: 60, bpm: 148),
            Sample(start: morning.addingTimeInterval(-3 * day), meters: 5_200, metersPerSecond: 3.4, hills: 15, bpm: 161),
            // No heart rate written, so its page shows the ring's series, or says there is none.
            Sample(start: morning.addingTimeInterval(-1 * day), meters: 8_150, metersPerSecond: 3.1, hills: 35, bpm: nil),
        ]
    }

    private static func write(_ run: Sample, to store: HKHealthStore) async throws {
        let configuration = HKWorkoutConfiguration()
        configuration.activityType = .running
        configuration.locationType = .outdoor

        let builder = HKWorkoutBuilder(healthStore: store, configuration: configuration, device: .local())
        try await builder.beginCollection(at: run.start)
        try await builder.addSamples(samples(of: run))
        try await builder.endCollection(at: run.end)
        guard let workout = try await builder.finishWorkout() else { return }

        let route = HKWorkoutRouteBuilder(healthStore: store, device: .local())
        try await route.insertRouteData(locations(of: run))
        _ = try await route.finishRoute(with: workout, metadata: nil)
    }

    private static func samples(of run: Sample) -> [HKSample] {
        let distance = HKQuantitySample(
            type: HKQuantityType(.distanceWalkingRunning),
            quantity: HKQuantity(unit: .meter(), doubleValue: run.meters),
            start: run.start, end: run.end)
        guard let bpm = run.bpm else { return [distance] }

        let perMinute = HKUnit.count().unitDivided(by: .minute())
        let beats = stride(from: 0.0, to: run.seconds, by: 5).map { offset in
            let at = run.start.addingTimeInterval(offset)
            // Rising through the first ten minutes, then wandering a few beats either side.
            let warming = min(1, offset / 600)
            let rate = Double(bpm) - 25 * (1 - warming) + 4 * sin(offset / 90)
            return HKQuantitySample(
                type: HKQuantityType(.heartRate),
                quantity: HKQuantity(unit: perMinute, doubleValue: rate.rounded()),
                start: at, end: at)
        }
        return [distance] + beats
    }

    /// A loop around the invented meadow, one fix every two seconds, climbing and falling with
    /// the run's hills.
    private static func locations(of run: Sample) -> [CLLocation] {
        let centre = (latitude: 46.5, longitude: 8.25)
        let radius = run.meters / (2 * .pi)
        let metresPerDegree = 6_371_008.8 * .pi / 180
        return stride(from: 0.0, through: run.seconds, by: 2).map { offset in
            let angle = 2 * .pi * offset / run.seconds
            // Squashed into an egg so the loop does not look drawn with a compass.
            let north = radius * 0.8 * sin(angle)
            let east = radius * (cos(angle) + 0.15 * cos(2 * angle))
            return CLLocation(
                coordinate: CLLocationCoordinate2D(
                    latitude: centre.latitude + north / metresPerDegree,
                    longitude: centre.longitude + east / (metresPerDegree * cos(centre.latitude * .pi / 180))),
                altitude: 1200 + run.hills * sin(3 * angle),
                horizontalAccuracy: 5,
                verticalAccuracy: 3,
                course: -1,
                speed: run.metersPerSecond,
                timestamp: run.start.addingTimeInterval(offset))
        }
    }
}
#endif
