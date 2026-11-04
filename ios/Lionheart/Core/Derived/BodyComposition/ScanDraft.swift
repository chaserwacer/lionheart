import Foundation

/// The review form for one scan: every figure as the athlete sees and edits it, and which of the
/// analyser's panels the sheet carried.
///
/// Figures are kept as typed text, so a half-typed "21." stays what he typed. A figure the reading
/// could not make out starts empty; nothing here fills a box with a likely value.
struct ScanDraft: Equatable, Sendable {
    var device: String
    var unit: WeightUnit?
    var day: CalendarDay?
    var time: ClockTime?
    var entries: [SheetField: String]
    /// The panels the sheet carried. A panel the analyser does not print is switched off and sent
    /// as absent, rather than as a panel of empty boxes.
    var panels: Set<SheetPanel>
    /// What the reading passes saw for a figure they could not agree on, to show beside its box.
    var disagreements: [SheetField: [Double]]

    /// A form with nothing in it, for a sheet typed in by hand.
    static func blank(device: String = "") -> ScanDraft {
        ScanDraft(device: device, unit: nil, day: nil, time: nil, entries: [:], panels: [], disagreements: [:])
    }

    /// The form as a reading of a photographed sheet fills it: what was read, and every panel any
    /// figure was read from switched on.
    static func reading(_ reading: SheetReading) -> ScanDraft {
        let fields = Set(reading.values.keys).union(reading.disagreements.keys)
        return ScanDraft(
            device: reading.device ?? "",
            unit: reading.unit,
            day: reading.day,
            time: reading.time,
            entries: Dictionary(uniqueKeysWithValues: reading.values.map { ($0.key, Self.text($0.value, for: $0.key)) }),
            panels: Set(SheetPanel.allCases.filter { !Set($0.fields).isDisjoint(with: fields) }),
            disagreements: reading.disagreements)
    }

    /// The form as a recorded scan fills it, for a correction.
    static func correcting(_ scan: BodyCompositionScan) -> ScanDraft {
        let sheet = scan.sheet
        var entries: [SheetField: Double] = [
            .height: sheet.height.value, .weight: sheet.weight, .skeletalMuscleMass: sheet.skeletalMuscleMass,
            .bodyFatMass: sheet.bodyFatMass, .leanBodyMass: sheet.leanBodyMass,
            .percentBodyFat: sheet.percentBodyFat, .bodyMassIndex: sheet.bodyMassIndex,
        ]
        var panels = Set<SheetPanel>()
        for section in sheet.sections {
            switch section {
            case .bodyWater(let water):
                panels.insert(.bodyWater)
                entries[.intracellularWater] = water.intracellular
                entries[.extracellularWater] = water.extracellular
                entries[.totalBodyWater] = water.total
            case .dryLeanMass(let mass):
                panels.insert(.dryLeanMass)
                entries[.dryLeanMass] = mass
            case .segmentalLean(let segments):
                panels.insert(.segmentalLean)
                for segment in BodySegment.allCases {
                    entries[.leanMass(segment)] = segments[segment].mass
                    entries[.leanPercent(segment)] = segments[segment].percentOfReference
                }
            case .segmentalFat(let segments):
                panels.insert(.segmentalFat)
                for segment in BodySegment.allCases {
                    entries[.fatMass(segment)] = segments[segment].mass
                    entries[.fatPercent(segment)] = segments[segment].percentOfReference
                }
            case .extracellularWaterRatio(let ratio):
                panels.insert(.extracellularWaterRatio)
                entries[.extracellularWaterRatio] = ratio
            case .fitnessScore(let points):
                panels.insert(.fitnessScore)
                entries[.fitnessScore] = Double(points)
            case .massControl(let control):
                panels.insert(.massControl)
                entries[.fatControl] = control.bodyFat
                entries[.leanControl] = control.leanBody
            case .basalMetabolicRate(let kilocalories):
                panels.insert(.basalMetabolicRate)
                entries[.basalMetabolicRate] = Double(kilocalories)
            case .visceralFatLevel(let level):
                panels.insert(.visceralFatLevel)
                entries[.visceralFatLevel] = Double(level)
            case .impedance(let rows):
                panels.insert(.impedance)
                for row in rows {
                    for segment in BodySegment.allCases {
                        entries[.impedance(kilohertz: Int(row.kilohertz), segment)] = row.ohms[segment]
                    }
                }
            case .unrecognised:
                break
            }
        }
        return ScanDraft(
            device: sheet.device, unit: sheet.unit, day: scan.day, time: scan.time,
            entries: Dictionary(uniqueKeysWithValues: entries.map { ($0.key, Self.text($0.value, for: $0.key)) }),
            panels: panels, disagreements: [:])
    }

    // MARK: - Reading the form

    func number(_ field: SheetField) -> Double? {
        entries[field].flatMap(SetDraft.number)
    }

    /// The height's unit, which follows the sheet's: an analyser printing pounds prints inches.
    var heightUnit: LengthUnit { unit == .kilograms ? .centimetres : .inches }

    /// The figures the form asks for: the core, and every figure of every panel switched on.
    var askedFields: [SheetField] {
        [.height] + SheetField.core + SheetPanel.allCases.filter(panels.contains).flatMap(\.fields)
    }

    /// How many of the asked figures hold a number.
    var filledCount: Int { askedFields.filter { number($0) != nil }.count }

    /// Everything that keeps the form from being a sheet, in the order the form shows them.
    var problems: [DraftProblem] {
        var problems: [DraftProblem] = []
        if device.trimmingCharacters(in: .whitespaces).isEmpty { problems.append(.noDevice) }
        if unit == nil { problems.append(.noUnit) }
        if day == nil { problems.append(.noDay) }
        if time == nil { problems.append(.noTime) }
        for field in askedFields where !isOptionalImpedance(field) {
            let text = entries[field]?.trimmingCharacters(in: .whitespaces) ?? ""
            guard !text.isEmpty else { problems.append(.missing(field)); continue }
            guard let value = SetDraft.number(text) else { problems.append(.notANumber(field)); continue }
            if !bounds(of: field).contains(value) { problems.append(.implausible(field)) }
        }
        for kilohertz in SheetPanel.impedanceFrequencies where panels.contains(.impedance) {
            let row = BodySegment.allCases.map { number(.impedance(kilohertz: kilohertz, $0)) }
            if row.contains(where: { $0 != nil }), row.contains(where: { $0 == nil }) {
                problems.append(.partialImpedanceRow(kilohertz: kilohertz))
            }
        }
        if panels.contains(.impedance), impedanceRows.isEmpty { problems.append(.noImpedanceRows) }
        return problems
    }

    /// An impedance row may be left wholly empty — a 570 prints three, a crease can strike one — so
    /// its figures are checked as a row rather than one by one.
    private func isOptionalImpedance(_ field: SheetField) -> Bool {
        if case .impedance = field { true } else { false }
    }

    private var impedanceRows: [ImpedanceRow] {
        SheetPanel.impedanceFrequencies.compactMap { kilohertz in
            Segments { number(.impedance(kilohertz: kilohertz, $0)) }
                .map { ImpedanceRow(kilohertz: Double(kilohertz), ohms: $0) }
        }
    }

    /// The plausible range of a figure — wide enough for any body, narrow enough to catch a figure
    /// typed into the wrong box or a lost decimal point. The server holds the same bounds.
    private func bounds(of field: SheetField) -> ClosedRange<Double> {
        switch field {
        case .height: heightUnit == .centimetres ? 90...245 : 36...96
        case .weight, .skeletalMuscleMass, .bodyFatMass, .leanBodyMass,
             .intracellularWater, .extracellularWater, .totalBodyWater, .dryLeanMass:
            0.01...1000
        case .percentBodyFat: 0...100
        case .bodyMassIndex: 5...100
        case .leanMass, .fatMass: 0...1000
        case .leanPercent, .fatPercent: 1...1000
        case .extracellularWaterRatio: 0.2...0.6
        case .fitnessScore: 0...200
        case .fatControl, .leanControl: -1000...1000
        case .basalMetabolicRate: 300...10_000
        case .visceralFatLevel: 1...30
        case .impedance: 1...5000
        }
    }

    /// The sheet and its clock when the form is complete, `nil` while anything is in `problems`.
    var scan: (day: CalendarDay, time: ClockTime, sheet: BodyCompositionSheet)? {
        guard problems.isEmpty, let unit, let day, let time,
              let height = number(.height), let weight = number(.weight),
              let muscle = number(.skeletalMuscleMass), let fat = number(.bodyFatMass),
              let lean = number(.leanBodyMass), let percentFat = number(.percentBodyFat),
              let index = number(.bodyMassIndex)
        else { return nil }

        let sheet = BodyCompositionSheet(
            device: device.trimmingCharacters(in: .whitespaces),
            unit: unit,
            height: Height(value: height, unit: heightUnit),
            weight: weight,
            skeletalMuscleMass: muscle,
            bodyFatMass: fat,
            leanBodyMass: lean,
            percentBodyFat: percentFat,
            bodyMassIndex: index,
            sections: SheetPanel.allCases.filter(panels.contains).compactMap(section))
        return (day, time, sheet)
    }

    private func section(_ panel: SheetPanel) -> SheetSection? {
        func reading(_ mass: SheetField, _ percent: SheetField) -> SegmentReading? {
            guard let mass = number(mass), let percent = number(percent) else { return nil }
            return SegmentReading(mass: mass, percentOfReference: percent)
        }

        switch panel {
        case .bodyWater:
            guard let intracellular = number(.intracellularWater), let extracellular = number(.extracellularWater),
                  let total = number(.totalBodyWater) else { return nil }
            return .bodyWater(.init(intracellular: intracellular, extracellular: extracellular, total: total))
        case .dryLeanMass:
            return number(.dryLeanMass).map(SheetSection.dryLeanMass)
        case .segmentalLean:
            return Segments { reading(.leanMass($0), .leanPercent($0)) }.map(SheetSection.segmentalLean)
        case .segmentalFat:
            return Segments { reading(.fatMass($0), .fatPercent($0)) }.map(SheetSection.segmentalFat)
        case .extracellularWaterRatio:
            return number(.extracellularWaterRatio).map(SheetSection.extracellularWaterRatio)
        case .fitnessScore:
            return number(.fitnessScore).map { .fitnessScore(Int($0.rounded())) }
        case .massControl:
            guard let fat = number(.fatControl), let lean = number(.leanControl) else { return nil }
            return .massControl(.init(bodyFat: fat, leanBody: lean))
        case .basalMetabolicRate:
            return number(.basalMetabolicRate).map { .basalMetabolicRate(Int($0.rounded())) }
        case .visceralFatLevel:
            return number(.visceralFatLevel).map { .visceralFatLevel(Int($0.rounded())) }
        case .impedance:
            return impedanceRows.isEmpty ? nil : .impedance(impedanceRows)
        }
    }

    /// A figure as the form shows it, the way the sheet prints it: a whole number for a score, a
    /// level or a rate, and otherwise at least one place — 41.0, 9.02, 0.375.
    static func text(_ value: Double, for field: SheetField) -> String {
        let isWhole = field == .fitnessScore || field == .basalMetabolicRate || field == .visceralFatLevel
        let places: ClosedRange<Int> = isWhole ? 0...0 : 1...3
        return value.formatted(
            .number.precision(.fractionLength(places)).grouping(.never).locale(Locale(identifier: "en_US_POSIX")))
    }
}

/// What the scan's day already holds as its bodyweight, as far as the phone knows.
enum DayBodyweight: Equatable, Sendable {
    /// The series could not be read, or does not reach back to the day.
    case unknown
    case nothing
    case logged(BodyweightMeasurement)

    /// Whether the day holds a reading other than `weight` — the case the form has to ask about.
    func differs(from weight: Double, _ unit: WeightUnit) -> BodyweightMeasurement? {
        switch self {
        case .logged(let logged) where logged.weight != weight || logged.unit != unit: logged
        case .logged, .unknown, .nothing: nil
        }
    }
}

extension ScanDraft {
    /// Whether a scan's weight becomes its day's bodyweight until the athlete says otherwise.
    ///
    /// A new scan records it. A correction keeps whatever the day holds when that is not the scan's
    /// own weight — a morning reading he chose to keep over the scan's — and follows the corrected
    /// weight when the day's reading was the scan's.
    static func recordsBodyweight(correcting original: BodyCompositionSheet?, day: DayBodyweight) -> Bool {
        guard let original, case .logged(let logged) = day else { return true }
        return logged.weight == original.weight && logged.unit == original.unit
    }
}

/// Why a review form is not yet a sheet.
enum DraftProblem: Hashable, Sendable {
    case noDevice
    case noUnit
    case noDay
    case noTime
    case missing(SheetField)
    case notANumber(SheetField)
    case implausible(SheetField)
    case partialImpedanceRow(kilohertz: Int)
    case noImpedanceRows

    /// The figure the problem is about, so the form can mark its box.
    var field: SheetField? {
        switch self {
        case .missing(let field), .notANumber(let field), .implausible(let field): field
        case .noDevice, .noUnit, .noDay, .noTime, .partialImpedanceRow, .noImpedanceRows: nil
        }
    }
}
