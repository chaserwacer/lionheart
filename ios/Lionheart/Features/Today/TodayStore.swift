import Foundation
import Observation

/// The Today screen's data.
///
/// Two requests, in parallel. They are independent, there is a known number of them, and the screen
/// needs both — which is exactly what `async let` is for, and what `APIClient` being `Sendable`
/// exists to allow. One window of sessions answers three of the screen's four questions, so a
/// wider single read beats three narrow ones and guarantees the cards agree with each other.
///
/// Everything the screen decides is a pure function in `Core/Derived/`; this type does IO and turns
/// the outcome into one state case at one `do`/`catch`.
@Observable
@MainActor
final class TodayStore {
    /// How far back to look for the last completed session, and forward for the next planned one.
    ///
    /// Generous rather than tight: an athlete returning after two weeks off should still see what
    /// they last did, and a program planned a month ahead should still show its next session.
    private static let lookBack: TimeInterval = 90 * 86_400
    private static let lookAhead: TimeInterval = 45 * 86_400

    /// How far back to look for Oura data. A fortnight is enough to find the last synced day and
    /// short enough that a long gap reads as "no data" rather than as a stale score.
    private static let ouraLookBackDays = 14

    private(set) var state: LoadState<TodaySnapshot> = .loading
    private(set) var quickLog: QuickLogState = .idle

    private let sessions: TrainingSessionsAPI
    private let oura: OuraAPI
    private let interpretation: InterpretationAPI
    private let now: @Sendable () -> Date

    private var load: Task<Void, Never>?

    init(
        sessions: TrainingSessionsAPI,
        oura: OuraAPI,
        interpretation: InterpretationAPI,
        now: @escaping @Sendable () -> Date = { Date() }
    ) {
        self.sessions = sessions
        self.oura = oura
        self.interpretation = interpretation
        self.now = now
    }

    /// Loads the screen, replacing any load already running.
    ///
    /// The handle is kept so a pull to refresh during a slow first load does not leave two
    /// responses racing to set the state — the later one wins by construction instead of by luck.
    func refresh() async {
        load?.cancel()
        let task = Task { await self.reload() }
        load = task
        await task.value
    }

    /// Empties the screen. Registered with `AuthStore`, so signing out cannot leave one athlete's
    /// sessions on screen for the next one.
    func reset() {
        load?.cancel()
        load = nil
        state = .loading
        quickLog = .idle
    }

    private func reload() async {
        let moment = now()
        let today = CalendarDay(moment)

        do {
            async let window = sessions.sessions(
                from: moment.addingTimeInterval(-Self.lookBack),
                to: moment.addingTimeInterval(Self.lookAhead))
            async let ouraDays = oura.days(
                from: CalendarDay(moment.addingTimeInterval(-Double(Self.ouraLookBackDays) * 86_400)),
                to: today)

            let snapshot = TodaySnapshot.reading(
                sessions: try await window, ouraDays: try await ouraDays, today: today)
            state = state.applying(.success(snapshot))
        } catch is CancellationError {
            // The screen went away or a newer refresh took over. Nothing to say, and in particular
            // nothing to put on screen: the value already there is still the best one.
            return
        } catch {
            state = state.applying(.failure((try? LoadFailure.from(error)) ?? .unavailable(
                String(describing: error))))
        }
    }

    // MARK: - Quick log

    /// Sends `text` to the interpretation engine.
    ///
    /// Both outcomes of `POST /interpret` are successes. Being asked which movement a name meant is
    /// how an understood request ended — it is a state this screen reports, not a failure.
    func submitQuickLog(_ text: String) async {
        let words = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !words.isEmpty else { return }

        quickLog = .interpreting
        do {
            let outcome = try await interpretation.interpret(rawText: words, loggedAt: now())
            quickLog = QuickLogState.after(outcome)
            if case .committed = outcome { await refresh() }
        } catch is CancellationError {
            quickLog = .idle
        } catch {
            quickLog = .refused((try? LoadFailure.from(error)) ?? .unavailable(String(describing: error)))
        }
    }

    func dismissQuickLogOutcome() {
        quickLog = .idle
    }
}
