import HealthKit

/// Reads the figures Apple's devices write to Health — the watch's VO2max, resting heart rate, HRV,
/// breathing and walking heart rate, the phone's gait — with the weigh-ins and the date of birth.
///
/// Every reading of a window of days is read and handed over as a plain `HealthReading`, source and
/// all; which are Apple's own measurements, and how they make days, is `DailyMeasures` in `Core/`.
/// Readings rather than HealthKit's daily statistics because a statistic cannot say which device
/// measured it, and Oura writes its own HRV — a different statistic — under the same type.
struct MeasurementReader: Sendable {
    let store: HKHealthStore

    static let readTypes: Set<HKObjectType> = Set(
        HealthMetric.allCases.map { $0.quantityType as HKObjectType }
            + [HKQuantityType(.bodyMass), HKCharacteristicType(.dateOfBirth)])

    /// The oldest reading of any figure Health holds, where a first sync starts.
    func earliestReading() async throws -> Date? {
        var earliest: Date?
        for type in HealthMetric.allCases.map(\.quantityType) + [HKQuantityType(.bodyMass)] {
            let oldest = try await HKSampleQueryDescriptor(
                predicates: [.quantitySample(type: type)],
                sortDescriptors: [SortDescriptor(\.startDate)],
                limit: 1
            ).result(for: store).first?.startDate
            if let oldest, oldest < earliest ?? .distantFuture { earliest = oldest }
        }
        return earliest
    }

    /// Every reading of every figure that started in `[from, to)`.
    func readings(from: Date, to: Date) async throws -> [HealthReading] {
        var readings: [HealthReading] = []
        for metric in HealthMetric.allCases {
            let samples = try await samples(of: metric.quantityType, from: from, to: to)
            readings += samples.map { sample in
                HealthReading(
                    metric: metric,
                    at: sample.startDate,
                    value: sample.quantity.doubleValue(for: metric.healthUnit),
                    source: ReadingSource(
                        bundleIdentifier: sample.sourceRevision.source.bundleIdentifier,
                        productType: sample.sourceRevision.productType))
            }
        }
        return readings
    }

    /// Every weigh-in that started in `[from, to)`, from any source: a scale, the Health app, an app
    /// that copies the athlete's weight in.
    func weighIns(from: Date, to: Date) async throws -> [BodyMassReading] {
        try await samples(of: HKQuantityType(.bodyMass), from: from, to: to).map { sample in
            BodyMassReading(at: sample.startDate, kilograms: sample.quantity.doubleValue(for: .gramUnit(with: .kilo)))
        }
    }

    /// The date of birth the athlete entered in Health, or `nil` where none is set or reading it was
    /// not allowed — Health does not say which.
    func dateOfBirth() -> CalendarDay? {
        guard let components = try? store.dateOfBirthComponents(),
              let year = components.year, let month = components.month, let day = components.day
        else { return nil }
        return CalendarDay(year: year, month: month, day: day)
    }

    private func samples(of type: HKQuantityType, from: Date, to: Date) async throws -> [HKQuantitySample] {
        try await HKSampleQueryDescriptor(
            predicates: [.quantitySample(type: type, predicate: HKQuery.predicateForSamples(withStart: from, end: to))],
            sortDescriptors: [SortDescriptor(\.startDate)]
        ).result(for: store)
    }
}

extension HealthMetric {
    var quantityType: HKQuantityType {
        switch self {
        case .vo2Max: HKQuantityType(.vo2Max)
        case .restingHeartRate: HKQuantityType(.restingHeartRate)
        case .heartRateVariabilitySdnn: HKQuantityType(.heartRateVariabilitySDNN)
        case .respiratoryRate: HKQuantityType(.respiratoryRate)
        case .walkingHeartRateAverage: HKQuantityType(.walkingHeartRateAverage)
        case .walkingSpeed: HKQuantityType(.walkingSpeed)
        case .walkingStepLength: HKQuantityType(.walkingStepLength)
        case .walkingDoubleSupport: HKQuantityType(.walkingDoubleSupportPercentage)
        case .walkingAsymmetry: HKQuantityType(.walkingAsymmetryPercentage)
        case .walkingSteadiness: HKQuantityType(.appleWalkingSteadiness)
        case .sixMinuteWalkDistance: HKQuantityType(.sixMinuteWalkTestDistance)
        }
    }

    /// The unit the server keeps the figure in (`HealthMetric`).
    var healthUnit: HKUnit {
        let perMinute = HKUnit.count().unitDivided(by: .minute())
        return switch self {
        case .vo2Max: HKUnit.literUnit(with: .milli).unitDivided(by: .gramUnit(with: .kilo).unitMultiplied(by: .minute()))
        case .restingHeartRate, .walkingHeartRateAverage, .respiratoryRate: perMinute
        case .heartRateVariabilitySdnn: .secondUnit(with: .milli)
        case .walkingSpeed: .meter().unitDivided(by: .second())
        case .walkingStepLength, .sixMinuteWalkDistance: .meter()
        // Health's percent unit reads 0 to 1, the fraction the server keeps.
        case .walkingDoubleSupport, .walkingAsymmetry, .walkingSteadiness: .percent()
        }
    }
}
