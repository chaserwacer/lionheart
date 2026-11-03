import Foundation

/// Everything the History tab shows, read once from one window.
///
/// A named cluster rather than four properties on the store, for the reason `TodaySnapshot` is one:
/// the log, the trends and the records are one reading of a moment. Holding them separately would
/// let the chart show a session the log beneath it does not list.
///
/// The raw sessions travel with the derivations because the detail screen needs the session itself —
/// its sets, its period, its effort — and re-reading one session over the network to show what the
/// list already holds would be a request for data the device has.
struct HistorySnapshot: Equatable, Sendable {
    let log: SessionLog
    let trends: TrainingTrends
    let records: [SubjectRecords]
    let catalogue: MovementCatalogue
    private let sessionsById: [UUID: TrainingSession]

    var isEmpty: Bool { log.isEmpty && records.isEmpty }

    func session(_ id: UUID) -> TrainingSession? { sessionsById[id] }

    /// The records standing on one subject, if any still do.
    func records(on subject: RecordSubject) -> SubjectRecords? {
        records.first { $0.subject == subject }
    }

    static func reading(
        sessions: [TrainingSession],
        movements: [MovementData],
        records: [SubjectRecords],
        today: CalendarDay
    ) -> HistorySnapshot {
        let catalogue = MovementCatalogue(movements)
        return HistorySnapshot(
            log: SessionLog.reading(sessions: sessions, catalogue: catalogue, today: today),
            trends: TrainingTrends.reading(sessions: sessions, catalogue: catalogue, today: today),
            // A subject with nothing standing would be a row with nothing on it.
            records: records.filter { !$0.isEmpty },
            catalogue: catalogue,
            sessionsById: Dictionary(
                sessions.map { ($0.id, $0) }, uniquingKeysWith: { first, _ in first }))
    }
}
