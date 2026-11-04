#if DEBUG && targetEnvironment(simulator)
import CoreLocation
import HealthKit

/// Invented workouts written to this device's Health store — runs, a ride, an indoor ride, a hike, a
/// walk — so the import and the workout page can be seen working in a simulator, whose Health store
/// starts empty.
///
/// **Debug simulator builds only.** It is the one thing in the app that asks to write to Health,
/// and it is compiled into nothing else — not Release, and not a Debug build run on a phone, whose
/// Health store is the athlete's own and must never hold an invented workout. Every value is made
/// up: the loops circle an open meadow nobody here lives near, at paces, powers and heart rates no
/// one recorded.
enum SampleRuns {
    /// Writes the sample workouts, each ending before `now`, asking for write access first.
    static func write(to store: HKHealthStore, now: Date = Date()) async throws {
        let writes: Set<HKSampleType> = Set([
            HKObjectType.workoutType(),
            HKSeriesType.workoutRoute(),
            HKQuantityType(.heartRate),
            HKQuantityType(.distanceWalkingRunning),
            HKQuantityType(.distanceCycling),
        ] + Metric.allCases.map(\.type))
        try await store.requestAuthorization(toShare: writes, read: HealthLibrary.readTypes)

        for sample in samples(before: now) {
            try await write(sample, to: store)
        }
    }

    /// The measures a sample may carry beyond heart rate, each written every five seconds.
    private enum Metric: CaseIterable {
        case cyclingPower, cyclingCadence, cyclingSpeed, runningPower, groundContactTime, verticalOscillation, strideLength

        var type: HKQuantityType {
            switch self {
            case .cyclingPower: HKQuantityType(.cyclingPower)
            case .cyclingCadence: HKQuantityType(.cyclingCadence)
            case .cyclingSpeed: HKQuantityType(.cyclingSpeed)
            case .runningPower: HKQuantityType(.runningPower)
            case .groundContactTime: HKQuantityType(.runningGroundContactTime)
            case .verticalOscillation: HKQuantityType(.runningVerticalOscillation)
            case .strideLength: HKQuantityType(.runningStrideLength)
            }
        }

        var unit: HKUnit {
            switch self {
            case .cyclingPower, .runningPower: .watt()
            case .cyclingCadence: .count().unitDivided(by: .minute())
            case .cyclingSpeed: .meter().unitDivided(by: .second())
            case .groundContactTime: .secondUnit(with: .milli)
            case .verticalOscillation: .meterUnit(with: .centi)
            case .strideLength: .meter()
            }
        }

        /// An invented reading `offset` seconds in, wandering either side of a typical figure.
        func value(at offset: TimeInterval, speed: Double) -> Double {
            let wander = sin(offset / 70)
            return switch self {
            case .cyclingPower: 190 + 45 * wander + (Int(offset) % 300 < 30 ? 160 : 0)
            case .cyclingCadence: 86 + 6 * wander
            case .cyclingSpeed: speed * (1 + 0.12 * wander)
            case .runningPower: 255 + 20 * wander
            case .groundContactTime: 248 - 8 * wander
            case .verticalOscillation: 8.6 + 0.5 * wander
            case .strideLength: 1.12 + 0.05 * wander
            }
        }
    }

    /// One invented workout: a loop of a given length at an even speed over rolling ground, or the
    /// same on a trainer with no route.
    private struct Sample {
        let activity: HKWorkoutActivityType
        let indoor: Bool
        let start: Date
        let meters: Double
        let metersPerSecond: Double
        let hills: Double
        let bpm: Int?
        let metrics: [Metric]

        var seconds: TimeInterval { meters / metersPerSecond }
        var end: Date { start.addingTimeInterval(seconds) }
    }

    private static func samples(before now: Date) -> [Sample] {
        let day: TimeInterval = 86_400
        let morning = Calendar.current.startOfDay(for: now).addingTimeInterval(7 * 3600)
        let form: [Metric] = [.runningPower, .groundContactTime, .verticalOscillation, .strideLength]
        return [
            Sample(activity: .running, indoor: false, start: morning.addingTimeInterval(-9 * day), meters: 12_400, metersPerSecond: 2.9, hills: 60, bpm: 148, metrics: form),
            Sample(activity: .cycling, indoor: false, start: morning.addingTimeInterval(-6 * day), meters: 38_000, metersPerSecond: 8.2, hills: 90, bpm: 139, metrics: [.cyclingPower, .cyclingCadence, .cyclingSpeed]),
            Sample(activity: .running, indoor: false, start: morning.addingTimeInterval(-5 * day), meters: 5_200, metersPerSecond: 3.4, hills: 15, bpm: 161, metrics: []),
            Sample(activity: .hiking, indoor: false, start: morning.addingTimeInterval(-4 * day), meters: 11_000, metersPerSecond: 1.25, hills: 220, bpm: 118, metrics: []),
            Sample(activity: .cycling, indoor: true, start: morning.addingTimeInterval(-3 * day), meters: 22_000, metersPerSecond: 8.6, hills: 0, bpm: 152, metrics: [.cyclingPower, .cyclingCadence, .cyclingSpeed]),
            Sample(activity: .walking, indoor: false, start: morning.addingTimeInterval(-2 * day), meters: 3_600, metersPerSecond: 1.4, hills: 10, bpm: 98, metrics: []),
            // No heart rate written, so its page shows the ring's series, or says there is none.
            Sample(activity: .running, indoor: false, start: morning.addingTimeInterval(-1 * day), meters: 8_150, metersPerSecond: 3.1, hills: 35, bpm: nil, metrics: []),
        ]
    }

    private static func write(_ sample: Sample, to store: HKHealthStore) async throws {
        let configuration = HKWorkoutConfiguration()
        configuration.activityType = sample.activity
        configuration.locationType = sample.indoor ? .indoor : .outdoor

        let builder = HKWorkoutBuilder(healthStore: store, configuration: configuration, device: .local())
        try await builder.beginCollection(at: sample.start)
        try await builder.addSamples(samples(of: sample))
        try await builder.addMetadata([HKMetadataKeyIndoorWorkout: sample.indoor])
        try await builder.endCollection(at: sample.end)
        guard let workout = try await builder.finishWorkout(), !sample.indoor else { return }

        let route = HKWorkoutRouteBuilder(healthStore: store, device: .local())
        try await route.insertRouteData(locations(of: sample))
        _ = try await route.finishRoute(with: workout, metadata: nil)
    }

    private static func samples(of sample: Sample) -> [HKSample] {
        let distanceType = HKQuantityType(sample.activity == .cycling ? .distanceCycling : .distanceWalkingRunning)
        let distance = HKQuantitySample(
            type: distanceType,
            quantity: HKQuantity(unit: .meter(), doubleValue: sample.meters),
            start: sample.start, end: sample.end)
        let offsets = Array(stride(from: 0.0, to: sample.seconds, by: 5))

        let measured: [HKSample] = sample.metrics.flatMap { metric in
            offsets.map { offset in
                let at = sample.start.addingTimeInterval(offset)
                return HKQuantitySample(
                    type: metric.type,
                    quantity: HKQuantity(unit: metric.unit, doubleValue: metric.value(at: offset, speed: sample.metersPerSecond)),
                    start: at, end: at)
            }
        }
        guard let bpm = sample.bpm else { return [distance] + measured }

        let perMinute = HKUnit.count().unitDivided(by: .minute())
        let beats = offsets.map { offset in
            let at = sample.start.addingTimeInterval(offset)
            // Rising through the first ten minutes, then wandering a few beats either side.
            let warming = min(1, offset / 600)
            let rate = Double(bpm) - 25 * (1 - warming) + 4 * sin(offset / 90)
            return HKQuantitySample(
                type: HKQuantityType(.heartRate),
                quantity: HKQuantity(unit: perMinute, doubleValue: rate.rounded()),
                start: at, end: at)
        }
        return [distance] + measured + beats
    }

    /// A loop around the invented meadow, one fix every two seconds, climbing and falling with
    /// the workout's hills.
    private static func locations(of sample: Sample) -> [CLLocation] {
        let centre = (latitude: 46.5, longitude: 8.25)
        let radius = sample.meters / (2 * .pi)
        let metresPerDegree = 6_371_008.8 * .pi / 180
        return stride(from: 0.0, through: sample.seconds, by: 2).map { offset in
            let angle = 2 * .pi * offset / sample.seconds
            // Squashed into an egg so the loop does not look drawn with a compass.
            let north = radius * 0.8 * sin(angle)
            let east = radius * (cos(angle) + 0.15 * cos(2 * angle))
            return CLLocation(
                coordinate: CLLocationCoordinate2D(
                    latitude: centre.latitude + north / metresPerDegree,
                    longitude: centre.longitude + east / (metresPerDegree * cos(centre.latitude * .pi / 180))),
                altitude: 1200 + sample.hills * sin(3 * angle),
                horizontalAccuracy: 5,
                verticalAccuracy: 3,
                course: -1,
                speed: sample.metersPerSecond,
                timestamp: sample.start.addingTimeInterval(offset))
        }
    }
}
#endif
