import Foundation

/// The non-training day routes and the availability read (`docs/api-surface.md` → Non-training days).
///
/// Marking the same schedule twice answers 409 and removing another athlete's answers 404; both
/// arrive as `APIError` and are stated by the sheet, never retried.
struct AvailabilityAPI: Sendable {
    let client: APIClient

    private static let root = "/api/training/non-training-days"

    /// Every non-training day marked, the latest-starting first.
    func nonTrainingDays() async throws -> [NonTrainingDay] {
        try await client.get(Self.root, as: LenientList<NonTrainingDay>.self).elements
    }

    /// Each day of the range: open, planned or non-training.
    func availability(_ range: DayRange) async throws -> AvailabilityCalendar {
        try await client.get(
            "/api/training/availability",
            query: [
                URLQueryItem(name: "From", value: range.from.wire),
                URLQueryItem(name: "To", value: range.to.wire),
            ],
            as: AvailabilityCalendar.self)
    }

    func mark(_ draft: NonTrainingDayDraft) async throws -> NonTrainingDay {
        try await client.post(Self.root, body: MarkNonTrainingDayRequest(draft))
    }

    func remove(_ nonTrainingDayId: UUID) async throws {
        try await client.delete("\(Self.root)/\(nonTrainingDayId.uuidString)")
    }
}

/// `POST /api/training/non-training-days`.
private struct MarkNonTrainingDayRequest: Encodable, Sendable {
    let day: String
    /// `once` or `weekly`.
    let `repeat`: String
    let until: String?
    let note: String?

    init(_ draft: NonTrainingDayDraft) {
        day = draft.day.wire
        self.repeat = draft.repeatsWeekly ? "weekly" : "once"
        until = draft.repeatsWeekly ? draft.until?.wire : nil
        let trimmed = draft.note.trimmingCharacters(in: .whitespacesAndNewlines)
        note = trimmed.isEmpty ? nil : trimmed
    }
}
