import Foundation

// The text of a photographed sheet as the reader works with it: each recognised line with its
// misreadings of printed figures repaired, the figures on it placed where they sit, and the loose
// matching a recogniser's misread letters need.

/// One recognised line, repaired and measured.
struct PrintedLine: Equatable {
    let text: String
    let normalised: String
    let letters: String
    let x: Double
    let y: Double
    let width: Double
    let height: Double

    init(_ recognised: RecognizedLine) {
        self.init(text: recognised.text, x: recognised.x, y: recognised.y, width: recognised.width, height: recognised.height)
    }

    init(text: String, x: Double, y: Double, width: Double, height: Double) {
        self.text = text
        normalised = PrintedLine.normalise(text)
        letters = text.lowercased().filter { $0.isLetter && $0.isASCII }
        self.x = x
        self.y = y
        self.width = width
        self.height = height
    }

    var midY: Double { y + height / 2 }
    var maxX: Double { x + width }
    var maxY: Double { y + height }

    /// The numbers on the line, each placed at its share of the line's width.
    var numbers: [PrintedNumber] {
        PrintedNumber.all(in: normalised).map { number in
            number.placed(x: x + width * number.offset, midY: midY, height: height)
        }
    }

    func first(_ pattern: String, in text: String) -> String? {
        text.firstMatch(pattern).map { $0.joined(separator: "|") }
    }

    /// The repairs a recogniser's misreadings of a printed figure need: a comma for the point, a
    /// space after it, an O for a zero beside digits, and "lbs" read as "1bs" — which
    /// would otherwise turn 1.1 lbs into 1.11.
    static func normalise(_ text: String) -> String {
        [
            (#"(?<=\d)\s*[1lI|]bs\b"#, " lbs"),
            (#"(?<=\d),\s?(?=\d)"#, "."),
            (#"(?<=\d)\.\s+(?=\d)"#, "."),
            (#"(?<=\d\.)\s*[Oo](?![a-zA-Z])"#, "0"),
            (#"(?<=\d)[Oo](?=\d)"#, "0"),
        ].reduce(text) { text, repair in
            text.replacingOccurrences(of: repair.0, with: repair.1, options: .regularExpression)
        }
    }

    /// The page read in overlapping tiles sees a line twice where tiles overlap, and a line a tile
    /// edge cut through as a fragment of itself. The widest reading of each place on the page is
    /// kept, and anything lying mostly inside one already kept is dropped.
    static func deduplicated(_ lines: [PrintedLine]) -> [PrintedLine] {
        lines.sorted { $0.width > $1.width }.reduce(into: [PrintedLine]()) { kept, line in
            let area = line.width * line.height
            let covered = kept.contains { other in
                let overlapX = min(line.maxX, other.maxX) - max(line.x, other.x)
                let overlapY = min(line.maxY, other.maxY) - max(line.y, other.y)
                return overlapX > 0 && overlapY > 0 && overlapX * overlapY > area * 0.6
            }
            if !covered { kept.append(line) }
        }
    }

    /// A recogniser splits a figure printed over a bar's tick into two lines — "8." and "58" — and
    /// this puts it back together: two lines on one baseline, touching, the first ending in a
    /// decimal point or a digit and the second starting with a digit.
    static func joiningFragments(_ lines: [PrintedLine]) -> [PrintedLine] {
        let sorted = lines.sorted { ($0.y, $0.x) < ($1.y, $1.x) }
        var joined: [PrintedLine] = []
        var used = Set<Int>()
        for (index, line) in sorted.enumerated() where !used.contains(index) {
            let partner = sorted.indices.first { other in
                other != index && !used.contains(other)
                    && sorted[other].x > line.x
                    && sorted[other].x - line.maxX < 0.012
                    && abs(sorted[other].midY - line.midY) < min(line.height, sorted[other].height) * 0.5
                    && (line.normalised.last.map { $0 == "." } ?? false)
                    && (sorted[other].normalised.first?.isNumber ?? false)
            }
            if let partner {
                used.insert(partner)
                let other = sorted[partner]
                joined.append(PrintedLine(
                    text: line.text + other.text, x: line.x, y: min(line.y, other.y),
                    width: other.maxX - line.x, height: max(line.height, other.height)))
            } else {
                joined.append(line)
            }
        }
        return joined
    }
}

/// One figure on a recognised line, and where on the page it sits.
struct PrintedNumber {
    /// The figure without any sign before it. A sheet's figures are unsigned but for the two
    /// control figures, and a bar's edge read as a dash must not make a mass negative.
    let value: Double
    /// The figure with the sign printed before it, for the control figures.
    let signed: Double
    let decimals: Int
    /// Where it starts along its line, as a share of the line's characters.
    let offset: Double
    let x: Double
    let midY: Double
    let height: Double
    /// Whether "lbs" or "kg" follows it.
    let isPounds: Bool
    let isKilograms: Bool

    static func all(in text: String) -> [PrintedNumber] {
        guard let pattern = try? NSRegularExpression(pattern: #"([+\-−]?)(\d+(?:\.\d+)?)"#) else { return [] }
        let whole = text as NSString
        return pattern.matches(in: text, range: NSRange(location: 0, length: whole.length)).compactMap { match in
            let digits = whole.substring(with: match.range(at: 2))
            guard let value = Double(digits) else { return nil }
            let isNegative = match.range(at: 1).length > 0 && whole.substring(with: match.range(at: 1)) != "+"
            let after = whole.substring(from: match.range.location + match.range.length).lowercased()
                .trimmingCharacters(in: .whitespaces)
            return PrintedNumber(
                value: value,
                signed: isNegative ? -value : value,
                decimals: digits.split(separator: ".").dropFirst().first?.count ?? 0,
                offset: Double(match.range.location) / Double(max(whole.length, 1)),
                x: 0, midY: 0, height: 0,
                isPounds: after.hasPrefix("lbs"),
                isKilograms: after.hasPrefix("kg"))
        }
    }

    func placed(x: Double, midY: Double, height: Double) -> PrintedNumber {
        PrintedNumber(value: value, signed: signed, decimals: decimals, offset: offset, x: x, midY: midY, height: height,
               isPounds: isPounds, isKilograms: isKilograms)
    }

    /// Numbers grouped into columns by where they start, left to right.
    static func columns(_ numbers: [PrintedNumber]) -> [[PrintedNumber]] {
        numbers.sorted { $0.x < $1.x }.reduce(into: [[PrintedNumber]]()) { columns, number in
            if let last = columns.last?.last, number.x - last.x < 0.03 {
                columns[columns.count - 1].append(number)
            } else {
                columns.append([number])
            }
        }
    }

    /// Each number given to the row it sits nearest, provided no other number claims that row and
    /// it sits nearer that row than any other.
    static func assign(_ numbers: [PrintedNumber], to rows: [(SheetField, Double)]) -> [SheetField: PrintedNumber] {
        let claims = numbers.compactMap { number in
            rows.min { abs($0.1 - number.midY) < abs($1.1 - number.midY) }.map { ($0.0, number) }
        }
        return Dictionary(grouping: claims, by: \.0).compactMapValues { $0.count == 1 ? $0[0].1 : nil }
    }
}

// MARK: - Matching misread text

extension String {
    /// The capture groups of the first match, or `nil` when there is none.
    func firstMatch(_ pattern: String) -> [String]? {
        guard let expression = try? NSRegularExpression(pattern: pattern, options: [.caseInsensitive]) else { return nil }
        let whole = self as NSString
        guard let match = expression.firstMatch(in: self, range: NSRange(location: 0, length: whole.length)) else { return nil }
        return (1..<max(match.numberOfRanges, 1)).map { index in
            let range = match.range(at: index)
            return range.location == NSNotFound ? "" : whole.substring(with: range)
        }
    }

    /// Whether these letters contain `needle`, allowing a misread letter or two — a recogniser
    /// reads "Extracellular" as "Extraceilular" and "Weight" as "Welght".
    func fuzzilyContains(_ needle: String) -> Bool {
        if contains(needle) { return true }
        let tolerance = needle.count < 6 ? 0 : max(1, needle.count / 7)
        guard tolerance > 0 else { return false }
        let letters = Array(self), target = Array(needle)
        for length in (target.count - 1)...(target.count + 1) where length > 0 && length <= letters.count {
            for start in 0...(letters.count - length)
            where Array(letters[start..<(start + length)]).editDistance(to: target) <= tolerance {
                return true
            }
        }
        return false
    }

    func fuzzilyHasPrefix(_ needle: String) -> Bool {
        if hasPrefix(needle) { return true }
        let tolerance = needle.count < 5 ? 0 : 1
        let letters = Array(self)
        return tolerance > 0 && letters.count >= needle.count - 1
            && [needle.count - 1, needle.count, needle.count + 1].contains { length in
                length <= letters.count && Array(letters.prefix(length)).editDistance(to: Array(needle)) <= tolerance
            }
    }
}

private extension Array where Element == Character {
    func editDistance(to other: [Character]) -> Int {
        guard !isEmpty else { return other.count }
        guard !other.isEmpty else { return count }
        var previous = Array<Int>(0...other.count)
        for (i, mine) in enumerated() {
            var current = [i + 1] + Array<Int>(repeating: 0, count: other.count)
            for (j, theirs) in other.enumerated() {
                current[j + 1] = Swift.min(previous[j + 1] + 1, current[j] + 1, previous[j] + (mine == theirs ? 0 : 1))
            }
            previous = current
        }
        return previous[other.count]
    }
}
