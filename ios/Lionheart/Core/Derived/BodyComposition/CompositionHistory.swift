import Foundation

/// A figure every scan carries, followed from scan to scan.
enum CompositionMeasure: CaseIterable, Hashable, Sendable {
    case weight
    case skeletalMuscleMass
    case bodyFatMass
    case percentBodyFat
    case leanBodyMass
    case extracellularWaterRatio

    var title: String {
        switch self {
        case .weight: "Weight"
        case .skeletalMuscleMass: "Skeletal muscle"
        case .bodyFatMass: "Body fat mass"
        case .percentBodyFat: "Body fat"
        case .leanBodyMass: "Lean body mass"
        case .extracellularWaterRatio: "ECW/TBW"
        }
    }

    /// Whether the figure is a mass, which reads in pounds whatever unit the sheet printed.
    var isMass: Bool {
        switch self {
        case .weight, .skeletalMuscleMass, .bodyFatMass, .leanBodyMass: true
        case .percentBodyFat, .extracellularWaterRatio: false
        }
    }

    /// The figure on one sheet, a mass in pounds (`Imperial`) so scans printed in either unit sit
    /// on one line.
    func value(in sheet: BodyCompositionSheet) -> Double? {
        let printed: Double? = switch self {
        case .weight: sheet.weight
        case .skeletalMuscleMass: sheet.skeletalMuscleMass
        case .bodyFatMass: sheet.bodyFatMass
        case .percentBodyFat: sheet.percentBodyFat
        case .leanBodyMass: sheet.leanBodyMass
        case .extracellularWaterRatio: sheet.extracellularWaterRatio
        }
        return isMass ? printed.map { Imperial.pounds($0, from: sheet.unit) } : printed
    }
}

/// The athlete's scans, oldest first, and what they say across time.
struct CompositionHistory: Equatable, Sendable {
    let scans: [BodyCompositionScan]

    init(_ scans: [BodyCompositionScan]) {
        self.scans = scans.sorted { ($0.day, $0.time) < ($1.day, $1.time) }
    }

    var latest: BodyCompositionScan? { scans.last }
    var isEmpty: Bool { scans.isEmpty }

    /// The scan before `scan`, when there is one.
    func previous(to scan: BodyCompositionScan) -> BodyCompositionScan? {
        scans.firstIndex(of: scan).flatMap { $0 > 0 ? scans[$0 - 1] : nil }
    }

    /// A measure at every scan that carries it, a mass in pounds.
    func points(_ measure: CompositionMeasure) -> [MetricPoint] {
        scans.compactMap { scan in measure.value(in: scan.sheet).map { MetricPoint(day: scan.day, value: $0) } }
    }

    /// How far a measure moved from the scan before `scan` to `scan`, a mass in pounds, or `nil`
    /// with no scan before it carrying the measure.
    func change(_ measure: CompositionMeasure, at scan: BodyCompositionScan) -> Double? {
        guard let previous = previous(to: scan),
              let then = measure.value(in: previous.sheet), let now = measure.value(in: scan.sheet)
        else { return nil }
        return now - then
    }

    /// Segmental balance at every scan with a segmental lean panel, oldest first.
    var balances: [(day: CalendarDay, balance: SegmentalBalance)] {
        scans.compactMap { scan in scan.sheet.segmentalLean.map { (scan.day, SegmentalBalance.of($0)) } }
    }
}

/// How lean mass is shared out: left against right, and arms against trunk against legs.
///
/// Read from the segmental lean panel. The pattern is set against the analyser's own reference for
/// the athlete's height — its percentages — rather than against masses, because an arm and a leg
/// are not meant to weigh the same.
struct SegmentalBalance: Equatable, Sendable {
    /// Left minus right, in the sheet's unit.
    let armDifference: Double
    let legDifference: Double
    /// Left minus right, as a percentage of the right.
    let armDifferencePercent: Double
    let legDifferencePercent: Double
    /// The mean percentage of reference of the two arms, the trunk, and the two legs.
    let armsOfReference: Double
    let trunkOfReference: Double
    let legsOfReference: Double

    static func of(_ lean: Segments<SegmentReading>) -> SegmentalBalance {
        SegmentalBalance(
            armDifference: lean.leftArm.mass - lean.rightArm.mass,
            legDifference: lean.leftLeg.mass - lean.rightLeg.mass,
            armDifferencePercent: percent(lean.leftArm.mass - lean.rightArm.mass, of: lean.rightArm.mass),
            legDifferencePercent: percent(lean.leftLeg.mass - lean.rightLeg.mass, of: lean.rightLeg.mass),
            armsOfReference: (lean.leftArm.percentOfReference + lean.rightArm.percentOfReference) / 2,
            trunkOfReference: lean.trunk.percentOfReference,
            legsOfReference: (lean.leftLeg.percentOfReference + lean.rightLeg.percentOfReference) / 2)
    }

    private static func percent(_ difference: Double, of base: Double) -> Double {
        base > 0 ? difference / base * 100 : 0
    }
}
