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
    /// Run after a session is corrected, for a tab holding its own copy of the log — Today's.
    var afterRevision: () async -> Void = {}

    /// The ring and cardio reads behind the day and week pages, supplied from the root so every
    /// stack that registers this reaches the same ones.
    @Environment(DiaryStore.self) private var diary
    /// Draws a cardio session's heart-rate curve.
    @Environment(CardioStore.self) private var cardio
    /// A day's movement timeline.
    @Environment(HabitsStore.self) private var habits

    var body: some View {
        let snapshot = history.state.value
        let catalogue = snapshot?.catalogue ?? library.catalogue

        switch route {
        case .session(let id):
            if let session = snapshot?.session(id) {
                SessionDetailView(
                    session: session, catalogue: catalogue, history: history, library: library,
                    afterRevision: afterRevision)
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
            ConsistencyView(history: history, diary: diary, library: library)
        case .focus:
            FocusView(focus: focus, history: history, library: library)
        case .day(let day):
            DayView(day: day, history: history, diary: diary, library: library)
        case .week(let start):
            WeekView(weekStarting: start, history: history, diary: diary)
        case .cardioSession(let id, let day):
            if let (session, maximum) = diary.cardioSession(id, on: day) {
                CardioSessionView(session: session, maxHeartRate: maximum, store: cardio)
            } else {
                ContentUnavailableView {
                    Text("Session not available")
                } description: {
                    Text("Open it again from its day.")
                }
            }
        case .night(let day):
            if let night = diary.days[day]?.value?.night {
                NightView(night: night)
            } else {
                ContentUnavailableView {
                    Text("Night not available")
                } description: {
                    Text("Open it again from its day.")
                }
            }
        case .dayActivity(let day):
            DayActivityView(day: day, store: habits)
        }
    }
}
