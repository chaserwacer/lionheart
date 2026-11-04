import Foundation

/// The decoding rules every wire type in this folder is built on.
///
/// Two of the server's habits shape all of it. Responses are camelCase with integer enums, and the
/// sealed hierarchies carry a `$type` discriminator — so a case the client has not heard of is a
/// thing that can arrive, and the only safe answer is a case for it rather than a thrown error that
/// takes the surrounding list down with it.
///
/// Every initialiser here is **total**: it returns a value for any JSON the container admits. What
/// an unrecognised or missing value *means* is decided where it is consumed, exhaustively.
enum Wire {
    /// The property name the server's polymorphic payloads discriminate on.
    static let discriminator = "$type"

    /// The decoder every response is read with. The server writes ISO-8601 with a fractional part
    /// and an offset for `DateTimeOffset`, and a bare `yyyy-MM-dd` for `DateOnly`; neither of
    /// Foundation's built-in strategies covers both, so dates are read per-property instead and
    /// this decoder keeps Foundation's defaults out of the way.
    static func decoder() -> JSONDecoder { JSONDecoder() }

    static func encoder() -> JSONEncoder { JSONEncoder() }
}

/// A container whose keys are not known at compile time, which is what reading `$type` needs.
struct WireKey: CodingKey, Hashable {
    let stringValue: String
    var intValue: Int? { nil }

    init(stringValue: String) { self.stringValue = stringValue }
    init?(intValue: Int) { nil }

    static let type = WireKey(stringValue: Wire.discriminator)
}

extension KeyedDecodingContainer where Key == WireKey {
    /// The discriminator, or the empty string when the payload carries none. A missing
    /// discriminator and an unknown one are the same problem, so they get the same answer.
    var wireType: String {
        (try? decode(String.self, forKey: .type)) ?? ""
    }

    /// The value at `key`, or `nil` when it is absent, null, or the wrong shape.
    ///
    /// This is the lenient read the endpoint types decode through: a member that drifts costs that
    /// member and not the screen. Use it for everything the type can render without, and a
    /// `required` read only where absence genuinely makes the value meaningless.
    func lenient<T: Decodable>(_ type: T.Type = T.self, _ key: String) -> T? {
        try? decodeIfPresent(T.self, forKey: WireKey(stringValue: key)) ?? nil
    }

    /// The value at `key`, or `fallback`.
    func lenient<T: Decodable>(_ key: String, or fallback: T) -> T {
        lenient(T.self, key) ?? fallback
    }

    /// The list at `key` with every element that failed to decode dropped, or an empty list.
    ///
    /// Dropping an element is the deliberate choice: the hierarchies here all have an unrecognised
    /// case, so an element only fails when the payload is malformed rather than merely unfamiliar,
    /// and one malformed row must not cost the other nineteen.
    func lenientList<T: Decodable>(_ type: T.Type = T.self, _ key: String) -> [T] {
        guard var list = try? nestedUnkeyedContainer(forKey: WireKey(stringValue: key)) else { return [] }

        var decoded: [T] = []
        while !list.isAtEnd {
            if let element = try? list.decode(T.self) {
                decoded.append(element)
            } else if (try? list.decode(SkippedElement.self)) == nil {
                // Neither read moved the cursor, so continuing would spin. Stop with what we have.
                return decoded
            }
        }
        return decoded
    }
}

/// Steps an unkeyed container past one element without reading it.
///
/// A throwing `decode` leaves the container's cursor where it was, so a malformed element would
/// otherwise be retried forever. An initialiser that cannot fail always advances it, which is the
/// entire job here — the element's contents are deliberately never looked at.
struct SkippedElement: Decodable {
    init(from decoder: any Decoder) throws {}
}

// MARK: - Dates

extension KeyedDecodingContainer where Key == WireKey {
    /// An instant written as ISO-8601 with an offset, read with the offset intact.
    func instant(_ key: String) -> Date? {
        lenient(String.self, key).flatMap(WireDate.instant(from:))
    }

    /// A `DateOnly`, which arrives as `yyyy-MM-dd` and means a calendar day the athlete lived,
    /// not an instant. It stays a `CalendarDay` so nothing downstream can accidentally read a
    /// time of day out of it.
    func day(_ key: String) -> CalendarDay? {
        lenient(String.self, key).flatMap(CalendarDay.init(wire:))
    }
}

/// Parsing for the two date spellings the server uses.
enum WireDate {
    /// ISO-8601 with or without fractional seconds. `DateTimeOffset` serializes with a fractional
    /// part only when it has one, so both have to parse or the first whole-second timestamp fails.
    static func instant(from wire: String) -> Date? {
        let spellings: [ISO8601DateFormatter.Options] = [
            [.withInternetDateTime, .withFractionalSeconds],
            [.withInternetDateTime],
        ]
        for options in spellings {
            let formatter = ISO8601DateFormatter()
            formatter.formatOptions = options
            if let date = formatter.date(from: wire) { return date }
        }

        // `DateTime` members (CreatedAt, an event's CreationTime) are written without an offset.
        // They are UTC on the server, so reading them as UTC is the faithful answer rather than a
        // guess about the device's zone.
        let plain = DateFormatter()
        plain.locale = Locale(identifier: "en_US_POSIX")
        plain.timeZone = TimeZone(secondsFromGMT: 0)
        for format in ["yyyy-MM-dd'T'HH:mm:ss.SSSSSSS", "yyyy-MM-dd'T'HH:mm:ss.SSS", "yyyy-MM-dd'T'HH:mm:ss"] {
            plain.dateFormat = format
            if let date = plain.date(from: wire) { return date }
        }
        return nil
    }

    /// An instant as UTC, for anything compared against stored instants — a window's bounds.
    static func wire(from instant: Date) -> String {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return formatter.string(from: instant)
    }

    /// An instant written **with the device's own UTC offset**, for a `DateTimeOffset` the server
    /// will derive a calendar day from.
    ///
    /// This is not interchangeable with `wire(from:)`, and sending UTC where this is wanted is a
    /// defect with no symptom until the clock goes past evening. The server reads a session's day
    /// at the offset it was logged at, on purpose, so that a session trained at eight in the
    /// evening west of Greenwich belongs to that evening and joins against that night's sleep. The
    /// same instant sent as `Z` is one in the morning, and the session lands on tomorrow.
    static func offsetWire(from instant: Date, in timeZone: TimeZone = .current) -> String {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        formatter.timeZone = timeZone
        return formatter.string(from: instant)
    }
}

/// A calendar day with no time of day and no zone — the client's `DateOnly`.
///
/// It is its own type rather than a `Date` because the server derived it at the offset the athlete
/// was living in (see `Period.LocalDate` on the backend), and turning it back into an instant here
/// would re-open exactly the question that derivation already answered. Nothing in the client is
/// allowed to ask what time of day a session's `Date` was.
struct CalendarDay: Hashable, Comparable, Sendable {
    let year: Int
    let month: Int
    let day: Int

    init(year: Int, month: Int, day: Int) {
        self.year = year
        self.month = month
        self.day = day
    }

    init?(wire: String) {
        let parts = wire.prefix(10).split(separator: "-")
        guard parts.count == 3,
              let year = Int(parts[0]), let month = Int(parts[1]), let day = Int(parts[2]) else { return nil }
        self.init(year: year, month: month, day: day)
    }

    /// The day `date` falls on in `calendar`'s time zone — how "today" enters the domain.
    init(_ date: Date, in calendar: Calendar = .current) {
        let parts = calendar.dateComponents([.year, .month, .day], from: date)
        self.init(year: parts.year ?? 1, month: parts.month ?? 1, day: parts.day ?? 1)
    }

    var wire: String { String(format: "%04d-%02d-%02d", year, month, day) }

    /// Noon on this day in `calendar`, which is what a date formatter needs. Noon rather than
    /// midnight so a zone shift of a few hours cannot move it to the day either side.
    func middayDate(in calendar: Calendar = .current) -> Date {
        calendar.date(from: DateComponents(year: year, month: month, day: day, hour: 12)) ?? Date()
    }

    /// Midnight at the start of this day in `calendar` — where a window of instants over the day
    /// begins.
    func startDate(in calendar: Calendar = .current) -> Date {
        calendar.startOfDay(for: middayDate(in: calendar))
    }

    /// The day `days` away, in the calendar's own day arithmetic so a daylight-saving change
    /// cannot skip or repeat one.
    func adding(days: Int, in calendar: Calendar = .current) -> CalendarDay {
        guard let moved = calendar.date(byAdding: .day, value: days, to: middayDate(in: calendar))
        else { return self }
        return CalendarDay(moved, in: calendar)
    }

    /// Whole days from this day to `other`: positive when `other` is later.
    func days(until other: CalendarDay, in calendar: Calendar = .current) -> Int {
        calendar.dateComponents([.day], from: startDate(in: calendar), to: other.startDate(in: calendar)).day ?? 0
    }

    static func < (lhs: Self, rhs: Self) -> Bool {
        (lhs.year, lhs.month, lhs.day) < (rhs.year, rhs.month, rhs.day)
    }
}

// MARK: - Durations

/// `TimeSpan` on the wire, which System.Text.Json writes as `[-][d.]hh:mm:ss[.fffffff]`.
enum WireDuration {
    static func seconds(from wire: String) -> Double? {
        var text = Substring(wire)
        let negative = text.first == "-"
        if negative { text = text.dropFirst() }

        // The optional day part is separated by a '.', the same character the fractional seconds
        // use, so it is only a day count when it appears before the first colon.
        var days = 0.0
        if let dot = text.firstIndex(of: "."), let colon = text.firstIndex(of: ":"), dot < colon {
            guard let parsed = Double(text[..<dot]) else { return nil }
            days = parsed
            text = text[text.index(after: dot)...]
        }

        let parts = text.split(separator: ":")
        guard parts.count == 3,
              let hours = Double(parts[0]), let minutes = Double(parts[1]), let seconds = Double(parts[2])
        else { return nil }

        let total = days * 86_400 + hours * 3_600 + minutes * 60 + seconds
        return negative ? -total : total
    }

    /// Whole seconds as `hh:mm:ss`, and a fraction to the millisecond after them where there is
    /// one — a set read back and sent again unchanged must say the same duration it was stored with.
    static func wire(fromSeconds seconds: Double) -> String {
        let milliseconds = Int((seconds * 1000).rounded())
        let whole = milliseconds / 1000
        let clock = String(format: "%02d:%02d:%02d", whole / 3600, (whole % 3600) / 60, whole % 60)
        return milliseconds % 1000 == 0 ? clock : clock + String(format: ".%03d", milliseconds % 1000)
    }
}

extension KeyedDecodingContainer where Key == WireKey {
    /// A `TimeSpan` as seconds.
    func duration(_ key: String) -> Double? {
        lenient(String.self, key).flatMap(WireDuration.seconds(from:))
    }
}
