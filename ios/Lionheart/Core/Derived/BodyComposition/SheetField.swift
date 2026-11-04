import Foundation

/// One figure on a body-composition sheet: what the reader looks for and the review form edits.
enum SheetField: Hashable, Sendable {
    case height
    case weight
    case skeletalMuscleMass
    case bodyFatMass
    case leanBodyMass
    case percentBodyFat
    case bodyMassIndex
    case intracellularWater
    case extracellularWater
    case totalBodyWater
    case dryLeanMass
    case leanMass(BodySegment)
    case leanPercent(BodySegment)
    case fatMass(BodySegment)
    case fatPercent(BodySegment)
    case extracellularWaterRatio
    case fitnessScore
    case fatControl
    case leanControl
    case basalMetabolicRate
    case visceralFatLevel
    case impedance(kilohertz: Int, BodySegment)

    /// The figures every analyser prints.
    static let core: [SheetField] = [
        .weight, .skeletalMuscleMass, .bodyFatMass, .leanBodyMass, .percentBodyFat, .bodyMassIndex,
    ]
}

/// A panel a sheet may carry, and the figures it holds.
enum SheetPanel: CaseIterable, Hashable, Sendable {
    case bodyWater
    case dryLeanMass
    case segmentalLean
    case segmentalFat
    case extracellularWaterRatio
    case fitnessScore
    case massControl
    case basalMetabolicRate
    case visceralFatLevel
    case impedance

    /// The frequencies the review form offers rows for: what a 570 prints. A sheet read with others
    /// keeps them as read.
    static let impedanceFrequencies = [5, 50, 500]

    var fields: [SheetField] {
        switch self {
        case .bodyWater: [.intracellularWater, .extracellularWater, .totalBodyWater]
        case .dryLeanMass: [.dryLeanMass]
        case .segmentalLean: BodySegment.allCases.flatMap { [.leanMass($0), .leanPercent($0)] }
        case .segmentalFat: BodySegment.allCases.flatMap { [.fatMass($0), .fatPercent($0)] }
        case .extracellularWaterRatio: [.extracellularWaterRatio]
        case .fitnessScore: [.fitnessScore]
        case .massControl: [.fatControl, .leanControl]
        case .basalMetabolicRate: [.basalMetabolicRate]
        case .visceralFatLevel: [.visceralFatLevel]
        case .impedance:
            Self.impedanceFrequencies.flatMap { kilohertz in BodySegment.allCases.map { .impedance(kilohertz: kilohertz, $0) } }
        }
    }
}
