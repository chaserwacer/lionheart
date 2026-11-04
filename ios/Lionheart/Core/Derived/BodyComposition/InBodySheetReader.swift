import Foundation

/// Reads an InBody result sheet's figures out of the text recognised on a photograph of it.
///
/// The sheet is read the way a person reads it: find a panel's heading, find a row's label inside
/// the panel, take the figure printed beside the label. Every rule below is a fact about how InBody
/// lays out its sheet — the bar charts print their value larger than their scale, segmental lean
/// prints the mass on the label's line and the percentage beneath it, the composition table puts
/// total body water between the intracellular and extracellular rows — so a figure that does not
/// sit where the layout puts it is left unread rather than taken from somewhere nearby.
///
/// Written against the 570's sheet. Other analysers share its headings and row labels, so a panel
/// they print the same way reads the same; one they lay out differently reads as absent, and the
/// review form shows it empty.
enum InBodySheetReader {
    static func read(_ recognised: [RecognizedLine]) -> SheetReading {
        let page = Page(recognised)
        var reading = SheetReading()
        reading.device = page.device
        reading.unit = page.unit
        reading.day = page.day
        reading.time = page.time
        reading.values = agreed([
            page.height,
            page.composition,
            page.muscleFat,
            page.segmentalLean,
            page.extracellularWaterRatio,
            page.fitnessScore,
            page.massControl,
            page.segmentalFat,
            page.basalMetabolicRate,
            page.visceralFatLevel,
            page.impedance,
        ])
        return reading
    }

    /// The panels' figures together. Weight and body fat mass are printed twice — in the table and
    /// on their bars — and two different readings of one figure on one page leave it unread.
    private static func agreed(_ panels: [[SheetField: Double]]) -> [SheetField: Double] {
        let readings = Dictionary(grouping: panels.flatMap { $0 }, by: \.key).mapValues { $0.map(\.value) }
        return readings.compactMapValues { values in
            values.allSatisfy { abs($0 - values[0]) < 0.0005 } ? values[0] : nil
        }
    }
}

// MARK: - The page

/// The recognised lines of one page, and the sheet's layout read off them.
private struct Page {
    let lines: [PrintedLine]

    init(_ recognised: [RecognizedLine]) {
        lines = PrintedLine.joiningFragments(PrintedLine.deduplicated(recognised.map(PrintedLine.init)))
    }

    /// The left column holds the composition tables and bar charts; the right, the scores,
    /// segmental fat and impedance.
    static let rightColumn = 0.64

    // Headings, found once.
    var compositionHeading: PrintedLine? { heading("bodycompositionanalysis", left: true) }
    var muscleFatHeading: PrintedLine? { heading("musclefatanalysis", left: true) }
    var obesityHeading: PrintedLine? { heading("obesityanalysis", left: true) }
    var segmentalLeanHeading: PrintedLine? { heading("segmentalleananalysis", left: true) }
    var waterRatioHeading: PrintedLine? { heading("ecwtbwanalysis", left: true) }
    var historyHeading: PrintedLine? { heading("bodycompositionhistory", left: true) }
    var fitnessHeading: PrintedLine? { heading("fitnessscore", left: false) }
    var controlHeading: PrintedLine? { heading("bodyfatleanbodymasscontrol", left: false) }
    var segmentalFatHeading: PrintedLine? { heading("segmentalfatanalysis", left: false) }
    var metabolicHeading: PrintedLine? { heading("basalmetabolicrate", left: false) }
    var visceralHeading: PrintedLine? { heading("visceralfatlevel", left: false) }
    var interpretationHeading: PrintedLine? { heading("resultsinterpretation", left: false) }
    var impedanceHeading: PrintedLine? { heading("impedance", left: false) }

    /// The topmost line in the column whose letters are the heading's. The right column repeats
    /// several headings in its interpretation notes further down; the panel is the first.
    private func heading(_ letters: String, left: Bool) -> PrintedLine? {
        lines
            .filter { (left ? $0.x < 0.5 : $0.x >= Self.rightColumn - 0.05) && $0.letters.fuzzilyContains(letters) }
            .min { $0.y < $1.y }
    }

    /// The lines between two headings in one column.
    private func panel(from top: PrintedLine?, to bottom: PrintedLine?, left: Bool) -> [PrintedLine] {
        guard let top else { return [] }
        let floor = bottom?.y ?? 1
        return lines.filter { line in
            line.midY > top.maxY - top.height * 0.2 && line.midY < floor
                && (left ? line.x < Self.rightColumn : line.maxX > Self.rightColumn)
        }
    }

    // MARK: Header

    private var header: [PrintedLine] {
        let floor = compositionHeading?.y ?? 0.15
        return lines.filter { $0.midY < floor }
    }

    var device: String? {
        // The "I" of InBody is the letter a recogniser most often drops or misreads.
        lines.lazy.compactMap { $0.first(#"n\s?body\s*(\d{3,4})(?!\d)"#, in: $0.normalised.lowercased()) }.first
            .map { "InBody\($0)" }
    }

    /// The unit the masses are printed in, from the unit labels beside them — at least two agreeing,
    /// so one stray reading of "kg" in a footnote decides nothing.
    var unit: WeightUnit? {
        let pounds = lines.filter { $0.normalised.lowercased().contains("lbs") }.count
        let kilograms = lines.filter { $0.normalised.lowercased().range(of: #"\(\s*kg\s*\)"#, options: .regularExpression) != nil }.count
        if pounds >= 2, pounds > kilograms { return .pounds }
        if kilograms >= 2, kilograms > pounds { return .kilograms }
        return nil
    }

    var time: ClockTime? {
        header.lazy.compactMap { line -> ClockTime? in
            guard let match = line.normalised.firstMatch(#"(?<!\d)(\d{1,2}):(\d{2})(?!\d)"#), match.count == 2,
                  let hour = Int(match[0]), let minute = Int(match[1]), hour < 24, minute < 60 else { return nil }
            return ClockTime(hour: hour, minute: minute)
        }.first
    }

    /// The test date, only in a spelling whose order is not in doubt: year first, or a day past 12.
    var day: CalendarDay? {
        header.lazy.compactMap { line -> CalendarDay? in
            if let match = line.normalised.firstMatch(#"(20\d{2})[.\-/]\s?(\d{1,2})[.\-/]\s?(\d{1,2})"#),
               let year = Int(match[0]), let month = Int(match[1]), let day = Int(match[2]) {
                return Self.calendarDay(year, month, day)
            }
            if let match = line.normalised.firstMatch(#"(\d{1,2})[.\-/]\s?(\d{1,2})[.\-/]\s?(20\d{2})"#),
               let first = Int(match[0]), let second = Int(match[1]), let year = Int(match[2]) {
                if first > 12 { return Self.calendarDay(year, second, first) }
                if second > 12 { return Self.calendarDay(year, first, second) }
            }
            return nil
        }.first
    }

    private static func calendarDay(_ year: Int, _ month: Int, _ day: Int) -> CalendarDay? {
        (1...12).contains(month) && (1...31).contains(day) ? CalendarDay(year: year, month: month, day: day) : nil
    }

    var height: [SheetField: Double] {
        for line in header {
            let text = line.normalised.lowercased()
            if let match = text.firstMatch(#"(\d)\s*ft\.?\s*(\d{1,2}(?:\.\d)?)\s*in"#), match.count == 2,
               let feet = Double(match[0]), let inches = Double(match[1]) {
                return [.height: feet * 12 + inches]
            }
            if let centimetres = line.first(#"(\d{3}(?:\.\d)?)\s*cm"#, in: text).flatMap(Double.init) {
                return [.height: centimetres]
            }
        }
        return [:]
    }

    // MARK: Body composition analysis

    /// The table: intracellular water, extracellular water, dry lean mass and body fat mass in a
    /// column against their labels, and the three sums in columns of their own to the right — total
    /// body water between the two waters, lean body mass below it, weight level with dry lean mass.
    /// The sums are read together or not at all: with one of the three columns missing, which
    /// column a figure stands in is no longer certain, and the weight would fill lean body mass's box.
    var composition: [SheetField: Double] {
        let rows = panel(from: compositionHeading, to: muscleFatHeading, left: true)
        let labels = rows.filter { $0.x < 0.3 }
        // By prefix, with one letter's slack: "intracellular" and "extracellular" are two letters apart.
        guard let intracellular = labels.first(where: { $0.letters.fuzzilyHasPrefix("intracellularwater") }),
              let extracellular = labels.first(where: { $0.letters.fuzzilyHasPrefix("extracellularwater") }),
              let dry = labels.first(where: { $0.letters.fuzzilyHasPrefix("dryleanmass") }),
              let fat = labels.first(where: { $0.letters.fuzzilyHasPrefix("bodyfatmass") })
        else { return [:] }

        let labelEdge = [intracellular, extracellular, dry, fat].map(\.maxX).max() ?? 0.3
        let figures = rows.flatMap(\.numbers).filter { $0.x > labelEdge && $0.decimals == 1 }
        let columns = PrintedNumber.columns(figures)
        guard let first = columns.first, first.count >= 2 else { return [:] }

        let column = PrintedNumber.assign(first, to: [
            (.intracellularWater, intracellular.midY), (.extracellularWater, extracellular.midY),
            (.dryLeanMass, dry.midY), (.bodyFatMass, fat.midY),
        ])
        return column.merging(Self.sums(columns.dropFirst(), intracellular, extracellular, dry)) { first, _ in first }
            .mapValues(\.value)
    }

    /// The three sums, when each stands alone in its own column and the columns run left to right
    /// in the order the rows they sit between run down.
    private static func sums(
        _ columns: ArraySlice<[PrintedNumber]>, _ intracellular: PrintedLine, _ extracellular: PrintedLine,
        _ dry: PrintedLine
    ) -> [SheetField: PrintedNumber] {
        guard columns.count == 3, columns.allSatisfy({ $0.count == 1 }) else { return [:] }
        let byColumn = columns.map { $0[0] }
        let byRow = PrintedNumber.assign(byColumn, to: [
            (.totalBodyWater, (intracellular.midY + extracellular.midY) / 2),
            (.leanBodyMass, (extracellular.midY + dry.midY) / 2),
            (.weight, dry.midY),
        ])
        let fields: [SheetField] = [.totalBodyWater, .leanBodyMass, .weight]
        let agree = zip(fields, byColumn).allSatisfy { field, number in byRow[field]?.x == number.x }
        return agree ? Dictionary(uniqueKeysWithValues: zip(fields, byColumn)) : [:]
    }

    // MARK: Muscle-fat and obesity

    /// Each bar's value is printed at the bar's end, larger than the scale above it.
    var muscleFat: [SheetField: Double] {
        let rows = panel(from: muscleFatHeading, to: segmentalLeanHeading, left: true)
        let labels: [(SheetField, PrintedLine)] = [
            (.weight, rows.first { $0.x < 0.2 && $0.letters.hasPrefix("weight") }),
            (.skeletalMuscleMass, rows.first { $0.x < 0.2 && ($0.letters == "smm" || $0.letters.hasPrefix("smm")) }),
            (.bodyFatMass, rows.first { $0.x < 0.2 && $0.letters.fuzzilyContains("bodyfatmass") }),
            (.bodyMassIndex, rows.first { $0.x < 0.2 && $0.letters.hasPrefix("bmi") }),
            (.percentBodyFat, rows.first { $0.x < 0.2 && $0.letters.hasPrefix("pbf") }),
        ].compactMap { field, line in line.map { (field, $0) } }.sorted { $0.1.y < $1.1.y }

        return labels.enumerated().reduce(into: [:]) { values, entry in
            let (index, (field, label)) = entry
            let floor = index + 1 < labels.count ? labels[index + 1].1.midY - labels[index + 1].1.height * 0.6 : 1
            let band = rows.filter {
                $0.midY >= label.midY - label.height * 0.6 && $0.midY < floor && $0.x > label.maxX + 0.05
            }
            if let value = Self.barValue(in: band, below: label, decimals: 1) { values[field] = value }
        }
    }

    /// A bar's value: the tallest line in the row holding a single figure printed to the bar's
    /// places, standing taller than the scale's lone numbers around it.
    ///
    /// The scale prints its labels along one line above the bar and the value at the bar's end,
    /// below that line, so a figure level with the scale numbers beside it is a scale label
    /// whatever its size — the BMI and body-fat scales print labels to one place, as the value is.
    /// It is measured against the numbers beside it rather than against the row's label, so a photo
    /// taken at a slight angle tilts both alike. A run of scale numbers read as one line is as tall
    /// as its tallest glyph, so it is no measure of their size.
    private static func barValue(in band: [PrintedLine], below label: PrintedLine, decimals: Int) -> Double? {
        let lone = band.filter { $0.numbers.count == 1 }
        let candidates = lone.filter {
            $0.numbers[0].decimals == decimals && sitsBelowItsScale($0, in: band, label: label)
        }
        guard let tallest = candidates.max(by: { $0.height < $1.height }) else { return nil }
        let others = lone.filter { $0 != tallest }.map(\.height).sorted()
        if !others.isEmpty, tallest.height < others[others.count / 2] * 1.15 { return nil }
        return tallest.numbers[0].value
    }

    /// Whether a figure sits half its height or more below the scale numbers within a tenth of the
    /// page of it, or, with none there to measure by, below the row's label.
    private static func sitsBelowItsScale(_ figure: PrintedLine, in band: [PrintedLine], label: PrintedLine) -> Bool {
        let beside = band.filter { $0 != figure && !$0.numbers.isEmpty && abs($0.x - figure.x) <= 0.1 }
        guard !beside.isEmpty else { return figure.midY - label.midY >= label.height * 0.3 }
        let scaleLine = beside.map(\.midY).reduce(0, +) / Double(beside.count)
        return figure.midY - scaleLine >= figure.height * 0.5
    }

    // MARK: Segmental lean

    /// Each segment prints its mass on its label's line and its percentage of reference beneath.
    var segmentalLean: [SheetField: Double] {
        let rows = panel(from: segmentalLeanHeading, to: waterRatioHeading, left: true)
        return Self.segmentRows(rows, labelsLeftOf: 0.2).reduce(into: [:]) { values, row in
            let figures = row.band.flatMap(\.numbers).filter { $0.x > 0.25 && $0.decimals >= 1 }
            let masses = figures.filter { $0.midY - row.label.midY <= row.label.height * 0.4 }
            let percents = figures.filter { $0.midY - row.label.midY > row.label.height * 0.4 }
            guard masses.count <= 1, percents.count <= 1 else { return }
            let mass = masses.first?.value
            let percent = percents.first?.value.flatMapIn(40...300)
            if let mass, percent.map({ mass < $0 }) ?? true { values[.leanMass(row.segment)] = mass }
            if let percent { values[.leanPercent(row.segment)] = percent }
        }
    }

    /// The five segment labels in a panel, each with the lines of its row.
    private static func segmentRows(_ rows: [PrintedLine], labelsLeftOf edge: Double, from left: Double = 0)
        -> [(segment: BodySegment, label: PrintedLine, band: [PrintedLine])] {
        let names: [(BodySegment, String)] = [
            (.rightArm, "rightarm"), (.leftArm, "leftarm"), (.trunk, "trunk"), (.rightLeg, "rightleg"), (.leftLeg, "leftleg"),
        ]
        let labels = names.compactMap { segment, letters in
            rows.first { $0.x >= left && $0.x < edge && $0.letters.fuzzilyHasPrefix(letters) }.map { (segment, $0) }
        }.sorted { $0.1.y < $1.1.y }

        return labels.enumerated().map { index, entry in
            let (segment, label) = entry
            let floor = index + 1 < labels.count ? labels[index + 1].1.midY - labels[index + 1].1.height * 0.7 : 1
            let band = rows.filter { $0.midY >= label.midY - label.height * 0.7 && $0.midY < floor }
            return (segment, label, band)
        }
    }

    // MARK: ECW/TBW

    var extracellularWaterRatio: [SheetField: Double] {
        let rows = panel(from: waterRatioHeading, to: historyHeading, left: true)
        guard let label = rows.first(where: { $0.x < 0.2 && $0.letters == "ecwtbw" }) else { return [:] }
        let value = Self.barValue(in: rows.filter { $0.x > label.maxX }, below: label, decimals: 3)?.flatMapIn(0.2...0.6)
        return value.map { [.extracellularWaterRatio: $0] } ?? [:]
    }

    // MARK: Right column

    /// "84/100 Points". Only a two- or three-digit score reads: a crease that loses a digit leaves
    /// one, and 8 is not a score.
    var fitnessScore: [SheetField: Double] {
        let rows = panel(from: fitnessHeading, to: controlHeading, left: false)
        let points = rows.lazy.compactMap { $0.first(#"(?<!\d)(\d{2,3})\s*/+\s*100"#, in: $0.normalised) }.first
        return points.flatMap(Double.init).map { [.fitnessScore: $0] } ?? [:]
    }

    var massControl: [SheetField: Double] {
        let rows = panel(from: controlHeading, to: segmentalFatHeading, left: false)
        let pairs: [(SheetField, String)] = [(.fatControl, "bodyfatmass"), (.leanControl, "leanbodymass")]
        return pairs.reduce(into: [:]) { values, pair in
            guard let label = rows.first(where: { $0.letters.fuzzilyHasPrefix(pair.1) }) else { return }
            let beside = rows.flatMap(\.numbers).filter {
                $0.x > label.x + 0.12 && abs($0.midY - label.midY) <= label.height * 0.7 && $0.decimals == 1
            }
            if beside.count == 1 { values[pair.0] = beside[0].signed }
        }
    }

    /// Each segment prints "( 1.1lbs)" then its percentage of reference.
    var segmentalFat: [SheetField: Double] {
        let rows = panel(from: segmentalFatHeading, to: metabolicHeading, left: false)
        return Self.segmentRows(rows, labelsLeftOf: 0.8, from: Self.rightColumn - 0.05).reduce(into: [:]) { values, row in
            let figures = row.band.flatMap(\.numbers).filter {
                abs($0.midY - row.label.midY) <= row.label.height * 0.7 && $0.decimals == 1
            }
            let masses = figures.filter(\.isPounds) + figures.filter { $0.isKilograms }
            let percents = figures.filter { !$0.isPounds && !$0.isKilograms && $0.x > row.label.x + 0.15 }
            if masses.count == 1 { values[.fatMass(row.segment)] = masses[0].value }
            if percents.count == 1, let percent = percents[0].value.flatMapIn(10...600) {
                values[.fatPercent(row.segment)] = percent
            }
        }
    }

    var basalMetabolicRate: [SheetField: Double] {
        let rows = panel(from: metabolicHeading, to: visceralHeading, left: false)
        let rates = rows.flatMap(\.numbers).filter { $0.decimals == 0 && (500...5000).contains($0.value) }
        return rates.count == 1 ? [.basalMetabolicRate: rates[0].value] : [:]
    }

    var visceralFatLevel: [SheetField: Double] {
        let rows = panel(from: visceralHeading, to: interpretationHeading, left: false)
        let level = rows.lazy.compactMap { $0.first(#"level\s*(\d{1,2})(?!\d)"#, in: $0.normalised.lowercased()) }.first
        return level.flatMap(Double.init).flatMap { $0.flatMapIn(1...30) }.map { [.visceralFatLevel: $0] } ?? [:]
    }

    /// One row per frequency, each read only when its own "kHz" marker is legible: assigning a row
    /// to a frequency by its position would be a guess about which rows the analyser printed.
    var impedance: [SheetField: Double] {
        let rows = panel(from: impedanceHeading, to: nil, left: false)
        return rows.reduce(into: [:]) { values, line in
            let text = line.normalised.replacingOccurrences(
                of: #"(?<![\d.])(\d{2,3}) (\d)(?![\d.])"#, with: "$1.$2", options: .regularExpression)
            guard let marker = text.range(of: #"(?<!\d)(\d{1,4})\s*k\s*hz"#, options: [.regularExpression, .caseInsensitive]),
                  let kilohertz = Int(text[marker].prefix { $0.isNumber }),
                  SheetPanel.impedanceFrequencies.contains(kilohertz) else { return }
            let ohms = PrintedNumber.all(in: String(text[marker.upperBound...])).filter { $0.decimals == 1 }.map(\.value)
            guard ohms.count == 5,
                  [ohms[0], ohms[1], ohms[3], ohms[4]].allSatisfy({ (100...1500).contains($0) }),
                  (3...100).contains(ohms[2]) else { return }
            for (segment, value) in zip(BodySegment.allCases, ohms) {
                values[.impedance(kilohertz: kilohertz, segment)] = value
            }
        }
    }
}

private extension Double {
    func flatMapIn(_ range: ClosedRange<Double>) -> Double? { range.contains(self) ? self : nil }
}
