import Foundation

// The words the composition screens use for a sheet's figures and panels, in one place so the form,
// the readout and the problems they state call each figure the same thing.

extension BodySegment {
    var title: String {
        switch self {
        case .rightArm: "Right arm"
        case .leftArm: "Left arm"
        case .trunk: "Trunk"
        case .rightLeg: "Right leg"
        case .leftLeg: "Left leg"
        }
    }

    var abbreviation: String {
        switch self {
        case .rightArm: "RA"
        case .leftArm: "LA"
        case .trunk: "TR"
        case .rightLeg: "RL"
        case .leftLeg: "LL"
        }
    }
}

extension SheetPanel {
    var title: String {
        switch self {
        case .bodyWater: "Body water"
        case .dryLeanMass: "Dry lean mass"
        case .segmentalLean: "Segmental lean"
        case .segmentalFat: "Segmental fat"
        case .extracellularWaterRatio: "ECW/TBW"
        case .fitnessScore: "Fitness score"
        case .massControl: "Fat and lean control"
        case .basalMetabolicRate: "Basal metabolic rate"
        case .visceralFatLevel: "Visceral fat level"
        case .impedance: "Impedance"
        }
    }
}

extension SheetField {
    var title: String {
        switch self {
        case .height: "Height"
        case .weight: "Weight"
        case .skeletalMuscleMass: "Skeletal muscle mass"
        case .bodyFatMass: "Body fat mass"
        case .leanBodyMass: "Lean body mass"
        case .percentBodyFat: "Body fat"
        case .bodyMassIndex: "BMI"
        case .intracellularWater: "Intracellular water"
        case .extracellularWater: "Extracellular water"
        case .totalBodyWater: "Total body water"
        case .dryLeanMass: "Dry lean mass"
        case .leanMass(let segment): "\(segment.title) lean"
        case .leanPercent(let segment): "\(segment.title) lean %"
        case .fatMass(let segment): "\(segment.title) fat"
        case .fatPercent(let segment): "\(segment.title) fat %"
        case .extracellularWaterRatio: "ECW/TBW"
        case .fitnessScore: "Fitness score"
        case .fatControl: "Fat control"
        case .leanControl: "Lean control"
        case .basalMetabolicRate: "Basal metabolic rate"
        case .visceralFatLevel: "Visceral fat level"
        case .impedance(let kilohertz, let segment): "\(kilohertz) kHz \(segment.abbreviation)"
        }
    }
}

extension DraftProblem {
    var message: String {
        switch self {
        case .noDevice: "Name the analyser, as the sheet prints it."
        case .noUnit: "Choose the unit the sheet prints."
        case .noDay: "Set the test date."
        case .noTime: "Set the test time."
        case .missing(let field): "\(field.title) is empty."
        case .notANumber(let field): "\(field.title) is not a number."
        case .implausible(let field): "\(field.title) is outside what a sheet prints."
        case .partialImpedanceRow(let kilohertz): "The \(kilohertz) kHz row is part filled; fill it or clear it."
        case .noImpedanceRows: "Impedance is on with no row filled."
        }
    }
}

extension SheetCheck {
    /// "BMI is 22.6; weight and height give 25.6."
    var message: String {
        let printed = ScanDraft.text(printed, for: kind.field)
        let computed = String(format: kind == .waterRatio ? "%.3f" : "%.1f", self.computed)
        return switch kind {
        case .bodyWater: "Total body water is \(printed); intracellular plus extracellular is \(computed)."
        case .leanBodyMass: "Lean body mass is \(printed); total body water plus dry lean mass is \(computed)."
        case .weight: "Weight is \(printed); lean body mass plus body fat mass is \(computed)."
        case .percentBodyFat: "Body fat is \(printed)%; body fat mass over weight is \(computed)%."
        case .bodyMassIndex: "BMI is \(printed); weight and height give \(computed)."
        case .waterRatio: "ECW/TBW is \(printed); the water figures give \(computed)."
        }
    }
}

/// A sheet's figures as the sheet prints them: a mass or a percentage to at least one place and
/// at most two — 41.0 lb, 9.02 lb, 16.5% — rather than trimmed to "39 lb".
enum SheetFigure {
    static func number(_ value: Double) -> String {
        value.formatted(.number.precision(.fractionLength(1...2)).grouping(.never))
    }

    /// A mass in pounds, whichever unit the sheet printed: "83.1 lb".
    static func mass(_ value: Double, _ unit: WeightUnit) -> String {
        labelled(number(Imperial.pounds(value, from: unit)), unit)
    }

    private static func labelled(_ figure: String, _ unit: WeightUnit) -> String {
        let label = Imperial.weightLabel(unit)
        return label.isEmpty ? figure : "\(figure) \(label)"
    }

    static func percent(_ value: Double) -> String { "\(number(value))%" }

    /// A suggested change: signed, with nought as nought rather than "±0".
    static func signedMass(_ value: Double, _ unit: WeightUnit) -> String {
        let magnitude = labelled(number(Imperial.pounds(abs(value), from: unit)), unit)
        return value == 0 ? magnitude : "\(value > 0 ? "+" : "−")\(magnitude)"
    }

}
