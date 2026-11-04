import CoreLocation
import HealthKit

/// Reads the runs, rides, hikes and walks other apps wrote to Apple Health, a page at a time after
/// an anchor, as offers for the server: each with its route, the recording app's own heart rate,
/// and whatever else it measured.
///
/// Only those four activities are read, since only they become training (the server's rule; see
/// `docs/apple-health.md`) and a route is the expensive part to send. Every app's are read — which
/// are left out, Oura's and Strong's among them, is the server's decision too, so it is written in
/// one place.
struct WorkoutReader: Sendable {
    let store: HKHealthStore

    static let readTypes: Set<HKObjectType> = Set([
        HKObjectType.workoutType(),
        HKSeriesType.workoutRoute(),
        HKQuantityType(.heartRate),
    ] + measured.map(\.type) + [HKQuantityType(.heartRateRecoveryOneMinute)])

    /// The activities read, in Health's types. The server decides what each becomes.
    static let activities: [HKWorkoutActivityType] = [.running, .cycling, .hiking, .walking]

    /// Which reading of Health the kept anchor belongs to. An anchor marks a place in one query's
    /// results, so reading another set of activities starts again from the beginning: the first sync
    /// after this changes reads the whole history once, and the server, idempotent on each workout,
    /// takes what is new and fills in the metrics of what it already holds.
    static let reading = 2

    /// Workouts per page. Each one's route is read before the page is handed over, so a page is
    /// small enough that a sync stopped partway has little to read again.
    static let pageSize = 10

    /// The runs added to Health after `anchor`, oldest first, and the anchor that follows them.
    func page(after anchor: Data?) async throws -> HealthWorkoutPage {
        let descriptor = HKAnchoredObjectQueryDescriptor(
            predicates: [.workout(NSCompoundPredicate(
                orPredicateWithSubpredicates: Self.activities.map(HKQuery.predicateForWorkouts(with:))))],
            anchor: try anchor.flatMap(Self.unarchived),
            limit: Self.pageSize)
        let result = try await descriptor.result(for: store)

        var offers: [HealthWorkoutImport] = []
        for workout in result.addedSamples.sorted(by: { $0.startDate < $1.startDate }) {
            offers.append(try await offer(of: workout))
        }
        return HealthWorkoutPage(
            workouts: offers,
            anchor: try Self.archived(result.newAnchor),
            isLast: result.addedSamples.count + result.deletedObjects.count < Self.pageSize)
    }

    /// The workouts Health still holds with these ids, oldest first — the ones the server turned
    /// down, offered again.
    func workouts(_ ids: [UUID]) async throws -> [HealthWorkoutImport] {
        guard !ids.isEmpty else { return [] }
        let held = try await HKSampleQueryDescriptor(
            predicates: [.workout(HKQuery.predicateForObjects(with: Set(ids)))],
            sortDescriptors: [SortDescriptor(\.startDate)]
        ).result(for: store)

        var offers: [HealthWorkoutImport] = []
        for workout in held {
            offers.append(try await offer(of: workout))
        }
        return offers
    }

    // MARK: - One workout

    private func offer(of workout: HKWorkout) async throws -> HealthWorkoutImport {
        async let route = route(of: workout)
        async let heartRate = heartRate(across: workout)
        async let metrics = metrics(of: workout)
        let source = workout.sourceRevision.source
        let metadata = workout.metadata ?? [:]

        return HealthWorkoutImport(
            workoutId: workout.uuid,
            activity: Self.activityName(workout.workoutActivityType),
            indoor: metadata[HKMetadataKeyIndoorWorkout] as? Bool ?? false,
            appName: source.name,
            appBundleId: source.bundleIdentifier,
            start: workout.startDate,
            end: workout.endDate,
            timeZone: (metadata[HKMetadataKeyTimeZone] as? String).flatMap(TimeZone.init(identifier:)) ?? .current,
            duration: workout.duration,
            distanceMeters: Self.distanceMeters(of: workout),
            elevationAscendedMeters: (metadata[HKMetadataKeyElevationAscended] as? HKQuantity)?.doubleValue(for: .meter()),
            route: try await route,
            heartRate: try await heartRate,
            metrics: try await metrics)
    }

    /// Every fix of every route the workout carries, oldest first. A fix with no valid position is
    /// dropped; one with no valid altitude keeps its position.
    private func route(of workout: HKWorkout) async throws -> [HealthWorkoutImport.RouteFix] {
        let routes = try await HKSampleQueryDescriptor(
            predicates: [.workoutRoute(HKQuery.predicateForObjects(from: workout))],
            sortDescriptors: [SortDescriptor(\.startDate)]
        ).result(for: store)

        var fixes: [HealthWorkoutImport.RouteFix] = []
        for route in routes {
            for try await location in HKWorkoutRouteQueryDescriptor(route).results(for: store)
            where location.horizontalAccuracy >= 0 {
                fixes.append(HealthWorkoutImport.RouteFix(
                    at: location.timestamp,
                    latitude: location.coordinate.latitude,
                    longitude: location.coordinate.longitude,
                    altitude: location.verticalAccuracy >= 0 ? location.altitude : nil))
            }
        }
        return fixes.sorted { $0.at < $1.at }
    }

    /// The heart rate the recording app itself wrote across the workout.
    ///
    /// Its own samples only: Oura writes the ring's heart rate to Health too, and that series is
    /// already Lionheart's from Oura directly, where the run page reads it when the app wrote none.
    private func heartRate(across workout: HKWorkout) async throws -> [HealthWorkoutImport.HeartRateReading] {
        let predicate = NSCompoundPredicate(andPredicateWithSubpredicates: [
            HKQuery.predicateForSamples(withStart: workout.startDate, end: workout.endDate),
            HKQuery.predicateForObjects(from: workout.sourceRevision.source),
        ])
        let samples = try await HKSampleQueryDescriptor(
            predicates: [.quantitySample(type: HKQuantityType(.heartRate), predicate: predicate)],
            sortDescriptors: [SortDescriptor(\.startDate)]
        ).result(for: store)

        let perMinute = HKUnit.count().unitDivided(by: .minute())
        return samples.map { sample in
            HealthWorkoutImport.HeartRateReading(
                at: sample.startDate, bpm: Int(sample.quantity.doubleValue(for: perMinute).rounded()))
        }
    }

    /// What the recording app measured beyond heart rate, a series per metric: a ride's power,
    /// cadence and speed, a run's power, speed and form, and for any workout the heart rate's
    /// one-minute recovery after it. The app's own samples, by the rule `heartRate(across:)` gives.
    private func metrics(of workout: HKWorkout) async throws -> [HealthWorkoutImport.MetricSeries] {
        var series: [HealthWorkoutImport.MetricSeries] = []
        for measure in Self.measured where measure.activities.contains(workout.workoutActivityType) {
            let samples = try await samples(of: measure.type, in: measure.unit, from: workout.startDate, to: workout.endDate, by: workout)
            if !samples.isEmpty { series.append(.init(metric: measure.metric, samples: samples)) }
        }
        // Written once the minute after the workout has passed, so it falls just past its end; the
        // first in the window, since a later one belongs to whatever the watch recorded next.
        let recovery = try await samples(
            of: HKQuantityType(.heartRateRecoveryOneMinute), in: Self.perMinute,
            from: workout.endDate, to: workout.endDate.addingTimeInterval(Self.recoveryWindow), by: workout)
        if let first = recovery.first { series.append(.init(metric: .heartRateRecovery, samples: [first])) }
        return series
    }

    private func samples(
        of type: HKQuantityType, in unit: HKUnit, from start: Date, to end: Date, by workout: HKWorkout
    ) async throws -> [MetricSample] {
        let predicate = NSCompoundPredicate(andPredicateWithSubpredicates: [
            HKQuery.predicateForSamples(withStart: start, end: end),
            HKQuery.predicateForObjects(from: workout.sourceRevision.source),
        ])
        return try await HKSampleQueryDescriptor(
            predicates: [.quantitySample(type: type, predicate: predicate)],
            sortDescriptors: [SortDescriptor(\.startDate)]
        ).result(for: store).map { sample in
            MetricSample(at: sample.startDate, value: sample.quantity.doubleValue(for: unit))
        }
    }

    /// How long after a workout its recovery reading may land.
    private static let recoveryWindow: TimeInterval = 15 * 60

    private static let perMinute = HKUnit.count().unitDivided(by: .minute())

    /// One metric Lionheart reads, in Health's type and in the unit the server holds it in.
    private struct Measure: Sendable {
        let metric: WorkoutMetric
        let type: HKQuantityType
        let unit: HKUnit
        let activities: Set<HKWorkoutActivityType>
    }

    private static let measured: [Measure] = [
        Measure(metric: .power, type: HKQuantityType(.cyclingPower), unit: .watt(), activities: [.cycling]),
        Measure(metric: .cadence, type: HKQuantityType(.cyclingCadence), unit: perMinute, activities: [.cycling]),
        Measure(metric: .speed, type: HKQuantityType(.cyclingSpeed), unit: .meter().unitDivided(by: .second()), activities: [.cycling]),
        Measure(metric: .power, type: HKQuantityType(.runningPower), unit: .watt(), activities: [.running]),
        Measure(metric: .speed, type: HKQuantityType(.runningSpeed), unit: .meter().unitDivided(by: .second()), activities: [.running]),
        Measure(metric: .groundContactTime, type: HKQuantityType(.runningGroundContactTime), unit: .secondUnit(with: .milli), activities: [.running]),
        Measure(metric: .verticalOscillation, type: HKQuantityType(.runningVerticalOscillation), unit: .meterUnit(with: .centi), activities: [.running]),
        Measure(metric: .strideLength, type: HKQuantityType(.runningStrideLength), unit: .meter(), activities: [.running]),
    ]

    /// The distance the app recorded: its summed distance samples where it wrote any — a ride's
    /// cycling distance, anything else's walking and running distance — else the total it stated on
    /// the workout itself, which is how an app writing a finished workout in one go records it.
    private static func distanceMeters(of workout: HKWorkout) -> Double? {
        let type = workout.workoutActivityType == .cycling
            ? HKQuantityType(.distanceCycling)
            : HKQuantityType(.distanceWalkingRunning)
        return workout.statistics(for: type)?.sumQuantity()?.doubleValue(for: .meter())
            ?? workout.totalDistance?.doubleValue(for: .meter())
    }

    /// Health's activity in the spelling the server reads, or its raw number for one not named here.
    private static func activityName(_ type: HKWorkoutActivityType) -> String {
        activityNames[type] ?? "activity-\(type.rawValue)"
    }

    private static let activityNames: [HKWorkoutActivityType: String] = [
        .running: "running",
        .walking: "walking",
        .hiking: "hiking",
        .cycling: "cycling",
    ]

    // MARK: - The anchor

    /// HealthKit's anchor, archived, with the reading it belongs to.
    private struct KeptAnchor: Codable {
        let reading: Int
        let anchor: Data
    }

    private static func archived(_ anchor: HKQueryAnchor) throws -> Data {
        try JSONEncoder().encode(KeptAnchor(
            reading: reading,
            anchor: try NSKeyedArchiver.archivedData(withRootObject: anchor, requiringSecureCoding: true)))
    }

    /// The kept anchor, or none — the beginning — for one kept by another reading, including the
    /// bare anchor a build reading runs alone kept.
    private static func unarchived(_ data: Data) throws -> HKQueryAnchor? {
        guard let kept = try? JSONDecoder().decode(KeptAnchor.self, from: data), kept.reading == reading else { return nil }
        return try NSKeyedUnarchiver.unarchivedObject(ofClass: HKQueryAnchor.self, from: kept.anchor)
    }
}
