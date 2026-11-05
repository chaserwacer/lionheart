import Foundation

/// An active injury as the week manages around it — the server's `ManagedInjury`: the families whose
/// targets it pauses and the trend in the weeks that led up to it.
struct ManagedInjury: Equatable, Sendable, Identifiable {
    let injuryId: UUID
    let name: String
    let since: CalendarDay
    /// The families it pauses: named on it, or trained by a movement its check-ins name.
    let families: [PatternFamily]
    let lastPain: Int?
    /// Each paused family's hard sets in the four weeks before the injury against the eight before those.
    let leadUp: [FamilyTrend]

    var id: UUID { injuryId }
}

/// A family's mean weekly hard sets in the four weeks before a day against the eight before those.
struct FamilyTrend: Equatable, Sendable {
    let family: PatternFamily
    let recent: Double
    let earlier: Double
    /// The rise as a share: 0.4 is forty per cent up; zero with nothing earlier to compare.
    let change: Double
}

extension ManagedInjury: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let id = container.lenient(UUID.self, "injuryId"), let since = container.day("since") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A managed injury needs its id and its day."))
        }
        self.init(
            injuryId: id,
            name: container.lenient("name", or: ""),
            since: since,
            families: container.lenientList(String.self, "families").map(PatternFamily.init(wire:)),
            lastPain: container.lenient(Int.self, "lastPain"),
            leadUp: container.lenientList(FamilyTrend.self, "preceding"))
    }
}

extension FamilyTrend: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            family: PatternFamily(wire: container.lenient("family", or: "")),
            recent: container.lenient("recent", or: 0),
            earlier: container.lenient("earlier", or: 0),
            change: container.lenient("change", or: 0))
    }
}
