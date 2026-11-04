import CoreLocation
import HealthKit

/// Reads the runs other apps wrote to Apple Health, a page at a time after an anchor, as offers for
/// the server: each with its route and the recording app's own heart rate.
///
/// Only runs are read, since only runs become training (the server's rule; see
/// `docs/apple-health.md`) and a route is the expensive part to send. Every app's runs are read —
/// which are left out, Oura's and Strong's among them, is the server's decision too, so it is
/// written in one place.
struct WorkoutReader: Sendable {
    let store: HKHealthStore

    static let readTypes: Set<HKObjectType> = [
        HKObjectType.workoutType(),
        HKSeriesType.workoutRoute(),
        HKQuantityType(.heartRate),
    ]

    /// Workouts per page. Each one's route is read before the page is handed over, so a page is
    /// small enough that a sync stopped partway has little to read again.
    static let pageSize = 10

    /// The runs added to Health after `anchor`, oldest first, and the anchor that follows them.
    func page(after anchor: Data?) async throws -> HealthWorkoutPage {
        let descriptor = HKAnchoredObjectQueryDescriptor(
            predicates: [.workout(HKQuery.predicateForWorkouts(with: .running))],
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

    // MARK: - One workout

    private func offer(of workout: HKWorkout) async throws -> HealthWorkoutImport {
        async let route = route(of: workout)
        async let heartRate = heartRate(across: workout)
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
            heartRate: try await heartRate)
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

    /// The distance the app recorded: its summed distance samples where it wrote any, else the
    /// total it stated on the workout itself, which is how an app writing a finished workout in one
    /// go records it.
    private static func distanceMeters(of workout: HKWorkout) -> Double? {
        workout.statistics(for: HKQuantityType(.distanceWalkingRunning))?.sumQuantity()?.doubleValue(for: .meter())
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

    private static func archived(_ anchor: HKQueryAnchor) throws -> Data {
        try NSKeyedArchiver.archivedData(withRootObject: anchor, requiringSecureCoding: true)
    }

    private static func unarchived(_ data: Data) throws -> HKQueryAnchor? {
        try NSKeyedUnarchiver.unarchivedObject(ofClass: HKQueryAnchor.self, from: data)
    }
}
