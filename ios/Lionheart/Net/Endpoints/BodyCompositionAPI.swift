import Foundation

/// The body-composition scan routes.
///
/// A scan is sent whole, to record or to correct it, with the athlete's answer to what its weight
/// does to that day's bodyweight. A scan at a minute already recorded answers 409 — the same scan
/// sent twice — which the store states rather than swallows.
struct BodyCompositionAPI: Sendable {
    let client: APIClient

    private static let root = "/api/body-composition/scans"

    /// Every scan, oldest first.
    func scans(remembering key: ReadKey) async throws -> Remembered<[BodyCompositionScan]> {
        try await client.get(Self.root, as: LenientList<BodyCompositionScan>.self, remembering: key)
            .map(\.elements)
    }

    func record(_ scan: ScanSubmission) async throws -> BodyCompositionScan {
        try await client.post(Self.root, body: scan)
    }

    func correct(_ id: UUID, to scan: ScanSubmission) async throws -> BodyCompositionScan {
        try await client.put("\(Self.root)/\(id.uuidString)", body: scan)
    }

    func delete(_ id: UUID) async throws {
        try await client.delete("\(Self.root)/\(id.uuidString)")
    }
}

/// A scan as the athlete states it: the sheet's clock, the sheet, and whether its weight becomes
/// that day's bodyweight.
struct ScanSubmission: Encodable, Sendable, Equatable {
    let day: CalendarDay
    let time: ClockTime
    let sheet: BodyCompositionSheet
    let recordsBodyweight: Bool

    func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: WireKey.self)
        try container.encode(day.wire, forKey: WireKey(stringValue: "day"))
        try container.encode(time.wire, forKey: WireKey(stringValue: "time"))
        try container.encode(sheet, forKey: WireKey(stringValue: "sheet"))
        try container.encode(recordsBodyweight, forKey: WireKey(stringValue: "recordsBodyweight"))
    }
}
