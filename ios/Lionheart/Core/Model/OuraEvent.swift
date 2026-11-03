import Foundation

/// One thing the ring recorded inside a day: a sleep period, a workout, a guided session, a tag.
///
/// Four collections share one envelope, so the client learns the shape once and each reading only
/// has to say what *it* holds. The server sends them that way for the same reason.
///
/// `day` travels **alongside** `period` rather than being derived from it, because Oura's own
/// attribution and the clock genuinely disagree: a night beginning before midnight is filed under
/// the waking day. Computing the day from the period here would quietly move a third of the
/// athlete's nights.
struct OuraEvent<Reading: Equatable & Sendable>: Equatable, Sendable, Identifiable {
    /// Oura's own id, stable across pulls. It is the identity because a day can hold several of
    /// these and nothing else tells two naps apart.
    let ouraId: UUID
    let day: CalendarDay
    let period: Period
    let reading: Reading

    var id: UUID { ouraId }
}

extension OuraEvent: Decodable where Reading: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)

        guard let ouraId = container.lenient(UUID.self, "ouraId"),
              let day = container.day("day"),
              let reading = container.lenient(Reading.self, "reading")
        else {
            // Without an id there is nothing to tell two of these apart; without a day there is
            // nowhere to put it; without its reading there is nothing to show. `LenientList` drops
            // the row and the rest of the window survives.
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "An Oura event needs an id, a day and a reading."))
        }

        self.init(
            ouraId: ouraId,
            day: day,
            period: container.lenient(Period.self, "period") ?? .unrecognised(""),
            reading: reading)
    }
}

/// An evenly spaced run of samples across one period — the overnight heart-rate and HRV curves.
///
/// The three members travel together because none means anything alone: numbers with no interval
/// and no start cannot be placed on a clock.
///
/// A sample is optional because **a gap is real data**. The ring records nothing while it is off
/// the finger or cannot get a reading, and roughly one overnight slot in seven is empty. Reading a
/// gap as zero would draw a heart rate of zero; dropping it would shift every later sample earlier
/// in the night.
struct OuraSampleSeries: Equatable, Sendable {
    let intervalSeconds: Double
    let samples: [Double?]
    let startedAt: Date?

    var isEmpty: Bool { samples.allSatisfy { $0 == nil } }

    /// The samples that exist, with where each sits on the clock — what a chart plots.
    var measured: [(at: Date, value: Double)] {
        runs.flatMap(\.self)
    }

    /// The samples that exist, split wherever the ring took no reading, so a chart draws each
    /// unbroken stretch as its own line and a gap stays a gap rather than a line across it.
    var runs: [[(at: Date, value: Double)]] {
        guard let startedAt else { return [] }
        return samples.enumerated().reduce(into: [[(at: Date, value: Double)]]()) { runs, element in
            guard let sample = element.element else {
                if runs.last?.isEmpty == false { runs.append([]) }
                return
            }
            let point = (at: startedAt.addingTimeInterval(intervalSeconds * Double(element.offset)), value: sample)
            if runs.isEmpty { runs.append([point]) } else { runs[runs.count - 1].append(point) }
        }
        .filter { !$0.isEmpty }
    }
}

extension OuraSampleSeries: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            intervalSeconds: container.lenient("intervalSeconds", or: 0),
            samples: container.lenient([Double?].self, "samples") ?? [],
            startedAt: container.instant("startedAt"))
    }
}

/// One sleep period the ring scored: a night, a nap, or a stretch of rest.
///
/// This is the richest recovery source there is, and it is a separate collection from the daily
/// sleep score precisely because that score flattens all of it into one number.
struct OuraSleepPeriod: Equatable, Sendable {
    /// Oura's own word for what the period was — a main sleep, a shorter one, a late nap, rest.
    /// Kept as the string Oura sent: its vocabulary is external and grows on its own schedule.
    let kind: String
    let timeInBedSeconds: Int
    let totalSleepSeconds: Int
    let deepSleepSeconds: Int
    let lightSleepSeconds: Int
    let remSleepSeconds: Int
    let awakeSeconds: Int
    let latencySeconds: Int
    let efficiency: Int
    let restlessPeriods: Int
    /// Mean heart rate across the period.
    ///
    /// Oura reports **zero rather than absent** on short periods it took no reading across, and it
    /// is carried through as it arrived. `heartRate` is all gaps on exactly those records, which
    /// is how a reader tells a real zero from a missing one.
    let averageHeartRate: Double?
    let lowestHeartRate: Int?
    let averageHrv: Int?
    let averageBreath: Double?
    let readinessScoreDelta: Int?
    let sleepScoreDelta: Int?
    /// The sleep stage per five-minute slot, as Oura's digit-per-slot encoding, undecoded. The
    /// digit-to-stage mapping is not derivable from the payloads, so decoding it here would bake
    /// an unverifiable assumption into the client.
    let hypnogram: String
    let heartRate: OuraSampleSeries
    let hrv: OuraSampleSeries
    /// Whether Oura treats this as the night's main sleep rather than a nap or a stretch of rest.
    /// Received rather than compared against a literal, so one place owns the spelling.
    let isMainSleep: Bool
}

extension OuraSleepPeriod: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            kind: container.lenient("kind", or: ""),
            timeInBedSeconds: container.lenient("timeInBedSeconds", or: 0),
            totalSleepSeconds: container.lenient("totalSleepSeconds", or: 0),
            deepSleepSeconds: container.lenient("deepSleepSeconds", or: 0),
            lightSleepSeconds: container.lenient("lightSleepSeconds", or: 0),
            remSleepSeconds: container.lenient("remSleepSeconds", or: 0),
            awakeSeconds: container.lenient("awakeSeconds", or: 0),
            latencySeconds: container.lenient("latencySeconds", or: 0),
            efficiency: container.lenient("efficiency", or: 0),
            restlessPeriods: container.lenient("restlessPeriods", or: 0),
            averageHeartRate: container.lenient(Double.self, "averageHeartRate"),
            lowestHeartRate: container.lenient(Int.self, "lowestHeartRate"),
            averageHrv: container.lenient(Int.self, "averageHrv"),
            averageBreath: container.lenient(Double.self, "averageBreath"),
            readinessScoreDelta: container.lenient(Int.self, "readinessScoreDelta"),
            sleepScoreDelta: container.lenient(Int.self, "sleepScoreDelta"),
            hypnogram: container.lenient("hypnogram", or: ""),
            heartRate: container.lenient(OuraSampleSeries.self, "heartRate") ?? .empty,
            hrv: container.lenient(OuraSampleSeries.self, "hrv") ?? .empty,
            isMainSleep: container.lenient("isMainSleep", or: false))
    }
}

extension OuraSampleSeries {
    static let empty = OuraSampleSeries(intervalSeconds: 0, samples: [], startedAt: nil)
}

/// One workout the ring saw — training load Lionheart did not record itself.
struct OuraWorkout: Equatable, Sendable {
    /// Oura's activity vocabulary, kept as the string it sent.
    let activity: String
    let intensity: String
    /// Whether the athlete entered this workout or confirmed one the ring proposed.
    let source: String
    let label: String?
    /// kcal, absent on the activities Oura cannot measure it for.
    let calories: Double?
    let distanceMeters: Double?

    /// The activity as a reader says it — "Strength training", not `strength_training`.
    var activityPhrase: String { OuraVocabulary.phrase(activity) }

    /// What was measured, in one line: distance, calories, intensity. A measurement Oura could not
    /// take is left out rather than printed as zero, which would read as a workout that covered no
    /// ground rather than one the ring does not measure distance for.
    var summary: String {
        var parts: [String] = []
        if let distanceMeters, distanceMeters >= 1 {
            parts.append(Self.distance(meters: distanceMeters))
        }
        if let calories, calories >= 1 {
            parts.append("\(Int(calories.rounded())) kcal")
        }
        parts.append(OuraVocabulary.phrase(intensity))
        return parts.joined(separator: " · ")
    }

    /// Metres under a kilometre, kilometres above it. Rounding a 40-metre walk to "0.0 km" reads
    /// as no distance at all, which is the opposite of what Oura measured.
    static func distance(meters: Double) -> String {
        meters < 1000
            ? "\(Int(meters.rounded())) m"
            : "\(MeasurementText.plain(meters / 1000)) km"
    }
}

/// Oura's own vocabularies as words.
///
/// They are open sets Oura extends on its own schedule, so this reshapes the spelling rather than
/// mapping a closed list: a value Oura adds still reads, where an enum would have to refuse it.
/// Oura writes some vocabularies in `snake_case` (intensities, stress summaries) and its workout
/// activities in `camelCase`, so both are taken apart: `strengthTraining` reads "Strength training".
enum OuraVocabulary {
    static func phrase(_ value: String) -> String {
        let words = value.reduce(into: "") { spelled, character in
            if character == "_" {
                spelled.append(" ")
            } else if character.isUppercase, let last = spelled.last, last != " " {
                spelled.append(" ")
                spelled.append(Character(character.lowercased()))
            } else {
                spelled.append(character)
            }
        }
        guard let first = words.first else { return value }
        return first.uppercased() + words.dropFirst()
    }
}

extension OuraWorkout: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            activity: container.lenient("activity", or: ""),
            intensity: container.lenient("intensity", or: ""),
            source: container.lenient("source", or: ""),
            label: container.lenient(String.self, "label")?.nonEmpty,
            calories: container.lenient(Double.self, "calories"),
            distanceMeters: container.lenient(Double.self, "distanceMeters"))
    }
}

/// Something the athlete tagged on the ring — sauna, alcohol, travel, a late coffee.
///
/// The closest thing in the whole Oura surface to the athlete's own account of a day, which is why
/// a collection of under a hundred records is worth a screen: a bad night explains itself when the
/// same night carries `tag_sleep_alcohol`.
struct OuraTag: Equatable, Sendable {
    /// Oura's tag codes, as sent. They read as `tag_sleep_sauna`; `OuraTag.phrase(for:)` is what
    /// turns one into words.
    let codes: [String]
    let text: String?

    /// A tag code as a reader says it. Oura prefixes its codes by where the tag was entered, which
    /// is bookkeeping rather than meaning, so the prefix goes before the words do.
    static func phrase(for code: String) -> String {
        let stripped = ["tag_generic_", "tag_sleep_", "tag_activity_"]
            .reduce(code) { $0.replacingOccurrences(of: $1, with: "") }
        return OuraVocabulary.phrase(stripped)
    }

    /// Every code as words, or the free text when the athlete typed one instead.
    var phrases: [String] {
        let named = codes.map(Self.phrase(for:))
        guard named.isEmpty else { return named }
        return text?.nonEmpty.map { [$0] } ?? []
    }
}

extension OuraTag: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            codes: container.lenientList(String.self, "codes"),
            text: container.lenient(String.self, "text")?.nonEmpty)
    }
}
