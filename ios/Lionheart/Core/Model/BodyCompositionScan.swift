import Foundation

/// One body-composition scan: the day and minute the analyser printed, and its sheet.
struct BodyCompositionScan: Equatable, Sendable, Identifiable {
    let id: UUID
    let day: CalendarDay
    let time: ClockTime
    let sheet: BodyCompositionSheet
}

extension BodyCompositionScan: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let id = container.lenient(UUID.self, "id"), let day = container.day("day"),
              let time = container.lenient(String.self, "time").flatMap(ClockTime.init(wire:)),
              let sheet = container.lenient(BodyCompositionSheet.self, "sheet")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A scan needs an id, a day, a time and a sheet."))
        }
        self.init(id: id, day: day, time: time, sheet: sheet)
    }
}

/// A time of day as a clock on the wall shows it — the server's `TimeOnly`, `HH:mm:ss`.
struct ClockTime: Hashable, Comparable, Sendable {
    let hour: Int
    let minute: Int

    init(hour: Int, minute: Int) {
        self.hour = hour
        self.minute = minute
    }

    init?(wire: String) {
        let parts = wire.split(separator: ":")
        guard parts.count >= 2, let hour = Int(parts[0]), let minute = Int(parts[1]),
              (0..<24).contains(hour), (0..<60).contains(minute) else { return nil }
        self.init(hour: hour, minute: minute)
    }

    init(_ date: Date, in calendar: Calendar = .current) {
        let parts = calendar.dateComponents([.hour, .minute], from: date)
        self.init(hour: parts.hour ?? 0, minute: parts.minute ?? 0)
    }

    var wire: String { String(format: "%02d:%02d:00", hour, minute) }

    /// `HH:mm`, as the sheet prints it.
    var text: String { String(format: "%02d:%02d", hour, minute) }

    /// This time on `day`, for a date picker.
    func date(on day: CalendarDay, in calendar: Calendar = .current) -> Date {
        calendar.date(bySettingHour: hour, minute: minute, second: 0, of: day.startDate(in: calendar)) ?? day.middayDate(in: calendar)
    }

    static func < (lhs: Self, rhs: Self) -> Bool { (lhs.hour, lhs.minute) < (rhs.hour, rhs.minute) }
}

/// What one scan printed: the figures every analyser prints, and the panels this one carried.
///
/// Masses are in `unit`, the unit the sheet printed, and never convert. A panel the analyser did
/// not print, or that could not be read off the paper, is absent from `sections`. The percentages
/// of reference are the analyser's own — its reference for the athlete's height and weight.
struct BodyCompositionSheet: Equatable, Sendable {
    let device: String
    let unit: WeightUnit
    let height: Height
    let weight: Double
    let skeletalMuscleMass: Double
    let bodyFatMass: Double
    let leanBodyMass: Double
    let percentBodyFat: Double
    let bodyMassIndex: Double
    let sections: [SheetSection]

    var bodyWater: SheetSection.BodyWater? {
        sections.lazy.compactMap { if case .bodyWater(let water) = $0 { water } else { nil } }.first
    }

    var dryLeanMass: Double? {
        sections.lazy.compactMap { if case .dryLeanMass(let mass) = $0 { mass } else { nil } }.first
    }

    var segmentalLean: Segments<SegmentReading>? {
        sections.lazy.compactMap { if case .segmentalLean(let lean) = $0 { lean } else { nil } }.first
    }

    var segmentalFat: Segments<SegmentReading>? {
        sections.lazy.compactMap { if case .segmentalFat(let fat) = $0 { fat } else { nil } }.first
    }

    var extracellularWaterRatio: Double? {
        sections.lazy.compactMap { if case .extracellularWaterRatio(let ratio) = $0 { ratio } else { nil } }.first
    }

    var fitnessScore: Int? {
        sections.lazy.compactMap { if case .fitnessScore(let points) = $0 { points } else { nil } }.first
    }

    var massControl: SheetSection.MassControl? {
        sections.lazy.compactMap { if case .massControl(let control) = $0 { control } else { nil } }.first
    }

    var basalMetabolicRate: Int? {
        sections.lazy.compactMap { if case .basalMetabolicRate(let rate) = $0 { rate } else { nil } }.first
    }

    var visceralFatLevel: Int? {
        sections.lazy.compactMap { if case .visceralFatLevel(let level) = $0 { level } else { nil } }.first
    }

    var impedance: [ImpedanceRow]? {
        sections.lazy.compactMap { if case .impedance(let rows) = $0 { rows } else { nil } }.first
    }

    /// Whether every panel is one this build can write back. A correction re-sends the sheet whole,
    /// so a sheet holding a panel this build cannot name is not offered for correction here.
    var isWhollyUnderstood: Bool {
        !sections.contains { if case .unrecognised = $0 { true } else { false } }
    }
}

/// The height the analyser was given — what its BMI and references were computed from.
struct Height: Equatable, Sendable {
    let value: Double
    let unit: LengthUnit
}

enum LengthUnit: Hashable, Sendable {
    case inches
    case centimetres
    case unrecognised(Int)

    init(wire: Int?) {
        switch wire {
        case 0: self = .inches
        case 1: self = .centimetres
        default: self = .unrecognised(wire ?? -1)
        }
    }

    var wire: Int {
        switch self {
        case .inches: 0
        case .centimetres: 1
        case .unrecognised(let value): value
        }
    }

    var abbreviation: String {
        switch self {
        case .inches: "in"
        case .centimetres: "cm"
        case .unrecognised: ""
        }
    }
}

/// The five segments an analyser measures, in the order it prints them.
enum BodySegment: CaseIterable, Hashable, Sendable {
    case rightArm, leftArm, trunk, rightLeg, leftLeg

    var wireName: String {
        switch self {
        case .rightArm: "rightArm"
        case .leftArm: "leftArm"
        case .trunk: "trunk"
        case .rightLeg: "rightLeg"
        case .leftLeg: "leftLeg"
        }
    }
}

/// A reading for each segment.
struct Segments<Reading> {
    let rightArm: Reading
    let leftArm: Reading
    let trunk: Reading
    let rightLeg: Reading
    let leftLeg: Reading

    init(rightArm: Reading, leftArm: Reading, trunk: Reading, rightLeg: Reading, leftLeg: Reading) {
        self.rightArm = rightArm
        self.leftArm = leftArm
        self.trunk = trunk
        self.rightLeg = rightLeg
        self.leftLeg = leftLeg
    }

    /// Built from a reading per segment, or `nil` when any segment has none.
    init?(_ reading: (BodySegment) -> Reading?) {
        guard let rightArm = reading(.rightArm), let leftArm = reading(.leftArm), let trunk = reading(.trunk),
              let rightLeg = reading(.rightLeg), let leftLeg = reading(.leftLeg) else { return nil }
        self.init(rightArm: rightArm, leftArm: leftArm, trunk: trunk, rightLeg: rightLeg, leftLeg: leftLeg)
    }

    subscript(segment: BodySegment) -> Reading {
        switch segment {
        case .rightArm: rightArm
        case .leftArm: leftArm
        case .trunk: trunk
        case .rightLeg: rightLeg
        case .leftLeg: leftLeg
        }
    }
}

extension Segments: Equatable where Reading: Equatable {}
extension Segments: Sendable where Reading: Sendable {}

/// One segment's mass, and that mass as a percentage of the analyser's own reference for it.
struct SegmentReading: Equatable, Sendable {
    let mass: Double
    let percentOfReference: Double
}

/// One frequency's row of segmental impedance, in ohms.
struct ImpedanceRow: Equatable, Sendable {
    let kilohertz: Double
    let ohms: Segments<Double>
}

/// A panel a sheet may carry, depending on the analyser that printed it.
enum SheetSection: Equatable, Sendable {
    case bodyWater(BodyWater)
    case dryLeanMass(Double)
    case segmentalLean(Segments<SegmentReading>)
    case segmentalFat(Segments<SegmentReading>)
    case extracellularWaterRatio(Double)
    case fitnessScore(Int)
    case massControl(MassControl)
    case basalMetabolicRate(Int)
    case visceralFatLevel(Int)
    case impedance([ImpedanceRow])
    case unrecognised(String)

    struct BodyWater: Equatable, Sendable {
        let intracellular: Double
        let extracellular: Double
        let total: Double
    }

    /// The change in fat and lean mass the analyser suggests: positive to gain, negative to lose.
    struct MassControl: Equatable, Sendable {
        let bodyFat: Double
        let leanBody: Double
    }
}

// MARK: - Wire

extension BodyCompositionSheet: Codable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let weight = container.lenient(Double.self, "weight"),
              let muscle = container.lenient(Double.self, "skeletalMuscleMass"),
              let fat = container.lenient(Double.self, "bodyFatMass"),
              let lean = container.lenient(Double.self, "leanBodyMass"),
              let percentFat = container.lenient(Double.self, "percentBodyFat"),
              let index = container.lenient(Double.self, "bodyMassIndex"),
              let height = container.lenient(Height.self, "height")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A sheet needs its core figures."))
        }
        self.init(
            device: container.lenient("device", or: ""),
            unit: WeightUnit(wire: container.lenient(Int.self, "unit")),
            height: height,
            weight: weight,
            skeletalMuscleMass: muscle,
            bodyFatMass: fat,
            leanBodyMass: lean,
            percentBodyFat: percentFat,
            bodyMassIndex: index,
            sections: container.lenientList(SheetSection.self, "sections"))
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: WireKey.self)
        try container.encode(device, forKey: WireKey(stringValue: "device"))
        try container.encode(unit.wire, forKey: WireKey(stringValue: "unit"))
        try container.encode(height, forKey: WireKey(stringValue: "height"))
        try container.encode(weight, forKey: WireKey(stringValue: "weight"))
        try container.encode(skeletalMuscleMass, forKey: WireKey(stringValue: "skeletalMuscleMass"))
        try container.encode(bodyFatMass, forKey: WireKey(stringValue: "bodyFatMass"))
        try container.encode(leanBodyMass, forKey: WireKey(stringValue: "leanBodyMass"))
        try container.encode(percentBodyFat, forKey: WireKey(stringValue: "percentBodyFat"))
        try container.encode(bodyMassIndex, forKey: WireKey(stringValue: "bodyMassIndex"))
        try container.encode(sections, forKey: WireKey(stringValue: "sections"))
    }
}

extension Height: Codable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let value = container.lenient(Double.self, "value") else {
            throw DecodingError.dataCorrupted(.init(codingPath: decoder.codingPath, debugDescription: "A height needs a value."))
        }
        self.init(value: value, unit: LengthUnit(wire: container.lenient(Int.self, "unit")))
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: WireKey.self)
        try container.encode(value, forKey: WireKey(stringValue: "value"))
        try container.encode(unit.wire, forKey: WireKey(stringValue: "unit"))
    }
}

extension Segments: Decodable where Reading: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let segments = Segments({ container.lenient(Reading.self, $0.wireName) }) else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "Every segment needs a reading."))
        }
        self = segments
    }
}

extension Segments: Encodable where Reading: Encodable {
    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: WireKey.self)
        for segment in BodySegment.allCases {
            try container.encode(self[segment], forKey: WireKey(stringValue: segment.wireName))
        }
    }
}

extension SegmentReading: Codable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let mass = container.lenient(Double.self, "mass"),
              let percent = container.lenient(Double.self, "percentOfReference") else {
            throw DecodingError.dataCorrupted(.init(codingPath: decoder.codingPath, debugDescription: "A segment needs both figures."))
        }
        self.init(mass: mass, percentOfReference: percent)
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: WireKey.self)
        try container.encode(mass, forKey: WireKey(stringValue: "mass"))
        try container.encode(percentOfReference, forKey: WireKey(stringValue: "percentOfReference"))
    }
}

extension ImpedanceRow: Codable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let kilohertz = container.lenient(Double.self, "kilohertz"),
              let ohms = container.lenient(Segments<Double>.self, "ohms") else {
            throw DecodingError.dataCorrupted(.init(codingPath: decoder.codingPath, debugDescription: "A row needs its frequency and ohms."))
        }
        self.init(kilohertz: kilohertz, ohms: ohms)
    }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: WireKey.self)
        try container.encode(kilohertz, forKey: WireKey(stringValue: "kilohertz"))
        try container.encode(ohms, forKey: WireKey(stringValue: "ohms"))
    }
}

extension SheetSection: Codable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType

        func required<T: Decodable>(_ key: String) throws -> T {
            guard let value = container.lenient(T.self, key) else {
                throw DecodingError.dataCorrupted(.init(
                    codingPath: decoder.codingPath, debugDescription: "A \(type) panel is missing \(key)."))
            }
            return value
        }

        switch type {
        case "bodyWater":
            self = .bodyWater(BodyWater(
                intracellular: try required("intracellular"),
                extracellular: try required("extracellular"),
                total: try required("total")))
        case "dryLeanMass": self = .dryLeanMass(try required("mass"))
        case "segmentalLean": self = .segmentalLean(try required("segments"))
        case "segmentalFat": self = .segmentalFat(try required("segments"))
        case "extracellularWaterRatio": self = .extracellularWaterRatio(try required("value"))
        case "fitnessScore": self = .fitnessScore(try required("points"))
        case "massControl":
            self = .massControl(MassControl(bodyFat: try required("bodyFat"), leanBody: try required("leanBody")))
        case "basalMetabolicRate": self = .basalMetabolicRate(try required("kilocalories"))
        case "visceralFatLevel": self = .visceralFatLevel(try required("level"))
        case "impedance": self = .impedance(container.lenientList(ImpedanceRow.self, "rows"))
        default: self = .unrecognised(type)
        }
    }

    /// An `.unrecognised` panel encodes as its bare discriminator, which the server refuses — and
    /// is why a sheet holding one is never offered for correction (`isWhollyUnderstood`).
    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: WireKey.self)
        func put(_ value: some Encodable, _ key: String) throws {
            try container.encode(value, forKey: WireKey(stringValue: key))
        }

        switch self {
        case .bodyWater(let water):
            try container.encode("bodyWater", forKey: .type)
            try put(water.intracellular, "intracellular")
            try put(water.extracellular, "extracellular")
            try put(water.total, "total")
        case .dryLeanMass(let mass):
            try container.encode("dryLeanMass", forKey: .type)
            try put(mass, "mass")
        case .segmentalLean(let segments):
            try container.encode("segmentalLean", forKey: .type)
            try put(segments, "segments")
        case .segmentalFat(let segments):
            try container.encode("segmentalFat", forKey: .type)
            try put(segments, "segments")
        case .extracellularWaterRatio(let ratio):
            try container.encode("extracellularWaterRatio", forKey: .type)
            try put(ratio, "value")
        case .fitnessScore(let points):
            try container.encode("fitnessScore", forKey: .type)
            try put(points, "points")
        case .massControl(let control):
            try container.encode("massControl", forKey: .type)
            try put(control.bodyFat, "bodyFat")
            try put(control.leanBody, "leanBody")
        case .basalMetabolicRate(let kilocalories):
            try container.encode("basalMetabolicRate", forKey: .type)
            try put(kilocalories, "kilocalories")
        case .visceralFatLevel(let level):
            try container.encode("visceralFatLevel", forKey: .type)
            try put(level, "level")
        case .impedance(let rows):
            try container.encode("impedance", forKey: .type)
            try put(rows, "rows")
        case .unrecognised(let type):
            try container.encode(type, forKey: .type)
        }
    }
}
