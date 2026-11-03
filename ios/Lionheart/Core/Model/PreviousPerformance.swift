import Foundation

/// What the athlete did on one movement the last time they trained it: that movement's sets from
/// the most recent completed session holding it, in the order performed.
///
/// It is the logging surface's "previous" column. The server answers it for every movement at once
/// so a workout started with no signal still has last time in front of it, from the remembered
/// read.
struct PreviousPerformance: Equatable, Sendable, Identifiable {
    let movementDataId: UUID
    let trainingSessionId: UUID
    /// The day that session belongs to, at the offset it was logged at.
    let date: CalendarDay
    let startedAt: Date?
    /// The movement's sets in that session, warm-ups included and marked.
    let sets: [SetEntry]

    var id: UUID { movementDataId }

    /// The sets that were the work, in order. Position in this list is what a set in the workout in
    /// flight is compared against: the second working set today against the second last time.
    var workingSets: [SetEntry] { sets.work }

    /// The working set at `position` (0-based), where last time went that deep.
    func workingSet(at position: Int) -> SetEntry? {
        let working = workingSets
        return working.indices.contains(position) ? working[position] : nil
    }
}

extension PreviousPerformance: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        guard let movementDataId = container.lenient(UUID.self, "movementDataId"),
              let trainingSessionId = container.lenient(UUID.self, "trainingSessionId"),
              let date = container.day("date")
        else {
            throw DecodingError.dataCorrupted(.init(
                codingPath: decoder.codingPath,
                debugDescription: "A previous performance needs a movement, a session and a date."))
        }
        self.movementDataId = movementDataId
        self.trainingSessionId = trainingSessionId
        self.date = date
        startedAt = container.instant("startedAt")
        sets = container.lenientList(SetEntry.self, "sets")
    }
}
