import SwiftUI

/// The screen for each `HistoryRoute`, built from the one reading of the log.
///
/// Its own view rather than a method on `HistoryView` because two stacks register it: History's,
/// and Today's, where a training line opens a movement's page. Each screen reads `HistoryStore` —
/// the whole log — so a movement looks the same whichever tab it was opened from.
struct HistoryDestination: View {
    let route: HistoryRoute
    let history: HistoryStore
    let focus: FocusStore
    let library: MovementLibraryStore

    var body: some View {
        let snapshot = history.state.value
        let catalogue = snapshot?.catalogue ?? library.catalogue

        switch route {
        case .session(let id):
            if let session = snapshot?.session(id) {
                SessionDetailView(session: session, catalogue: catalogue, history: history)
            } else {
                // Reachable: a refresh can land while a pushed row's session is no longer in the
                // window. Saying so beats an empty screen with a title.
                ContentUnavailableView {
                    Text("Session not available")
                } description: {
                    Text("It is no longer in the window History has loaded.")
                }
                .task { await history.refreshIfNeeded() }
            }
        case .records:
            PersonalRecordsView(records: snapshot?.records ?? [], catalogue: catalogue)
        case .recordSubject(let subject):
            SubjectRecordsView(
                subject: subject,
                held: snapshot?.records(on: subject),
                catalogue: catalogue,
                store: history)
        case .recordHistory(let key):
            RecordHistoryView(key: key, name: key.subject.name(in: catalogue), store: history)
        case .trends:
            TrendsView(trends: snapshot?.trends ?? TrainingTrends(weeks: [], strength: []))
        case .movement(let id):
            MovementView(movementDataId: id, history: history, focus: focus, library: library)
        case .movements:
            MovementsView(history: history)
        case .consistency:
            ConsistencyView(history: history)
        case .focus:
            FocusView(focus: focus, history: history, library: library)
        }
    }
}
