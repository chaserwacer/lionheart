import Foundation

/// A run of calendar days, both ends included — the server's `DayRange`.
struct DayRange: Equatable, Sendable {
    let from: CalendarDay
    let to: CalendarDay
}

extension DayRange: Codable {
    private enum CodingKeys: String, CodingKey { case from, to }

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(from.wire, forKey: .from)
        try container.encode(to.wire, forKey: .to)
    }

    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let from = container.day("from"), let to = container.day("to") else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath, debugDescription: "A day range needs both of its days."))
        }
        self.init(from: from, to: to)
    }
}

/// What one `POST /api/oura/sync` did: how many documents each collection returned and stored, and
/// the ones Oura sent that the server could not read.
///
/// The totals are read from the server's own `stored` and `rejected` rather than summed here, so the
/// client and the server cannot report different counts for one run.
struct OuraSyncReport: Equatable, Sendable {
    let range: DayRange?
    let stored: Int
    let rejected: Int
    let collections: [Collection]

    struct Collection: Equatable, Sendable {
        let name: String
        let received: Int
        let stored: Int
        /// The server's description of each document it could not read.
        let rejected: [String]
    }
}

extension OuraSyncReport: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let collections = container.lenientList(Collection.self, "collections")
        let range = container.lenient(RangeWire.self, "range")
        self.init(
            range: range.flatMap { wire in
                guard let from = wire.from, let to = wire.to else { return nil }
                return DayRange(from: from, to: to)
            },
            stored: container.lenient(Int.self, "stored") ?? collections.reduce(0) { $0 + $1.stored },
            rejected: container.lenient(Int.self, "rejected") ?? collections.reduce(0) { $0 + $1.rejected.count },
            collections: collections)
    }

    private struct RangeWire: Decodable {
        let from: CalendarDay?
        let to: CalendarDay?

        init(from decoder: any Decoder) throws {
            let container = try decoder.container(keyedBy: WireKey.self)
            from = container.day("from")
            to = container.day("to")
        }
    }
}

extension OuraSyncReport.Collection: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            name: container.lenient("collection", or: ""),
            received: container.lenient("received", or: 0),
            stored: container.lenient("stored", or: 0),
            rejected: container.lenient([String].self, "rejected") ?? [])
    }
}
