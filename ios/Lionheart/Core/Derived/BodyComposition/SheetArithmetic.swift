import Foundation

/// The sums a sheet's figures obey, checked on the review form.
///
/// A photographed sheet can be misread in a way no reading pass notices — a crease through one
/// digit of the BMI reads as the same wrong digit every time — but the misread figure then
/// disagrees with the figures it is computed from. The analyser prints each figure rounded, so a check allows for the rounding of
/// every term in it; a disagreement past that is a figure to look at on the paper.
struct SheetCheck: Equatable, Sendable {
    let kind: Kind
    /// What the sheet says, as entered.
    let printed: Double
    /// What the other figures give.
    let computed: Double

    enum Kind: CaseIterable, Sendable {
        /// Intracellular plus extracellular water is total body water.
        case bodyWater
        /// Total body water plus dry lean mass is lean body mass.
        case leanBodyMass
        /// Lean body mass plus body fat mass is weight.
        case weight
        /// Body fat mass over weight is percent body fat.
        case percentBodyFat
        /// Weight over height squared is BMI.
        case bodyMassIndex
        /// Extracellular over total body water is the ratio.
        case waterRatio

        /// The figure the check is printed as, which the form marks.
        var field: SheetField {
            switch self {
            case .bodyWater: .totalBodyWater
            case .leanBodyMass: .leanBodyMass
            case .weight: .weight
            case .percentBodyFat: .percentBodyFat
            case .bodyMassIndex: .bodyMassIndex
            case .waterRatio: .extracellularWaterRatio
            }
        }
    }

    /// Every check the form's figures fail. A check with a figure missing is not made.
    static func disagreements(in draft: ScanDraft) -> [SheetCheck] {
        Kind.allCases.compactMap { kind in
            guard let check = check(kind, in: draft) else { return nil }
            return abs(check.printed - check.computed) > tolerance(kind) ? check : nil
        }
    }

    private static func check(_ kind: Kind, in draft: ScanDraft) -> SheetCheck? {
        let n = draft.number
        func make(_ printed: Double?, _ computed: Double?) -> SheetCheck? {
            guard let printed, let computed, computed.isFinite else { return nil }
            return SheetCheck(kind: kind, printed: printed, computed: computed)
        }

        switch kind {
        case .bodyWater:
            guard draft.panels.contains(.bodyWater) else { return nil }
            return make(n(.totalBodyWater), sum(n(.intracellularWater), n(.extracellularWater)))
        case .leanBodyMass:
            guard draft.panels.contains(.bodyWater), draft.panels.contains(.dryLeanMass) else { return nil }
            return make(n(.leanBodyMass), sum(n(.totalBodyWater), n(.dryLeanMass)))
        case .weight:
            return make(n(.weight), sum(n(.leanBodyMass), n(.bodyFatMass)))
        case .percentBodyFat:
            guard let fat = n(.bodyFatMass), let weight = n(.weight), weight > 0 else { return nil }
            return make(n(.percentBodyFat), fat / weight * 100)
        case .bodyMassIndex:
            guard let weight = n(.weight), let height = n(.height), height > 0, let unit = draft.unit else { return nil }
            return make(n(.bodyMassIndex), bodyMassIndex(weight: weight, unit: unit, height: height))
        case .waterRatio:
            guard draft.panels.contains(.bodyWater), draft.panels.contains(.extracellularWaterRatio),
                  let extracellular = n(.extracellularWater), let total = n(.totalBodyWater), total > 0 else { return nil }
            return make(n(.extracellularWaterRatio), extracellular / total)
        }
    }

    private static func sum(_ first: Double?, _ second: Double?) -> Double? {
        guard let first, let second else { return nil }
        return first + second
    }

    /// BMI from a weight and height in the sheet's own units: pounds with inches, kilograms with
    /// centimetres.
    static func bodyMassIndex(weight: Double, unit: WeightUnit, height: Double) -> Double? {
        switch unit {
        case .pounds: 703.0696 * weight / (height * height)
        case .kilograms: weight / ((height / 100) * (height / 100))
        case .unrecognised: nil
        }
    }

    /// Each printed figure is rounded to its last place, so a sum of two of them can sit a
    /// rounding step from the printed total either way.
    private static func tolerance(_ kind: Kind) -> Double {
        switch kind {
        case .bodyWater, .leanBodyMass, .weight: 0.15
        case .percentBodyFat: 0.25
        case .bodyMassIndex: 0.2
        case .waterRatio: 0.003
        }
    }
}
