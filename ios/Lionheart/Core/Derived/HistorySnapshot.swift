import Foundation

/// Everything the History tab shows, read once from one window.
///
/// A named cluster rather than properties on the store, for the reason `TodaySnapshot` is one: the
/// log, the trends, the consistency reading and the records are one reading of a moment. Holding
/// them separately would let the chart show a session the log beneath it does not list.
///
/// The raw sessions travel with the derivations because the detail screen needs the session itself,
/// and a movement's page is a filter over all of them: the window is the athlete's whole log, so
/// re-reading any part of it over the network would be a request for data the device has.
struct HistorySnapshot: Equatable, Sendable {
    let log: SessionLog
    let trends: TrainingTrends
    let consistency: ConsistencyReading
    let records: [SubjectRecords]
    let movements: MovementIndex
    let catalogue: MovementCatalogue
    /// The window as read, every lifecycle state.
    let sessions: [TrainingSession]
    /// Every movement the sessions and records name, which the catalogue must hold to name them.
    let referencedMovements: Set<UUID>
    private let sessionsById: [UUID: TrainingSession]

    var isEmpty: Bool { log.isEmpty && records.isEmpty }

    func session(_ id: UUID) -> TrainingSession? { sessionsById[id] }

    /// The records standing on one subject, if any still do.
    func records(on subject: RecordSubject) -> SubjectRecords? {
        records.first { $0.subject == subject }
    }

    func history(of movementDataId: UUID, today: CalendarDay) -> MovementHistory {
        MovementHistory.of(movementDataId, in: sessions, today: today)
    }

    static func reading(
        sessions: [TrainingSession],
        catalogue: MovementCatalogue,
        records: [SubjectRecords],
        today: CalendarDay
    ) -> HistorySnapshot {
        HistorySnapshot(
            log: SessionLog.reading(sessions: sessions, catalogue: catalogue, today: today),
            trends: TrainingTrends.reading(sessions: sessions, catalogue: catalogue, today: today),
            consistency: ConsistencyReading.of(sessions: sessions, today: today),
            // A subject with nothing standing would be a row with nothing on it.
            records: records.filter { !$0.isEmpty },
            movements: MovementIndex.of(sessions, catalogue: catalogue, today: today),
            catalogue: catalogue,
            sessions: sessions,
            referencedMovements: sessions.reduce(into: Set(records.compactMap(\.subject.movementDataId))) {
                $0.formUnion($1.referencedMovementIds)
            },
            sessionsById: Dictionary(
                sessions.map { ($0.id, $0) }, uniquingKeysWith: { first, _ in first }))
    }
}
