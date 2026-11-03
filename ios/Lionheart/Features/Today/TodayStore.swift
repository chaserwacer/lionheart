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

    /// How far back to read Oura days and nights before the day shown: the baseline window, plus
    /// the days Today may fall back across when the ring has not synced this morning.
    private static let ouraLookBackDays = MetricBaseline.windowDays + TodaySnapshot.recoveryFallbackDays

    /// Which day the screen shows. Changed through `show(_:)`, which reloads.
    private(set) var selection: DaySelection = .today
    private(set) var state: LoadState<TodaySnapshot> = .loading
    private(set) var quickLog: QuickLogState = .idle

    /// Each figure's trend a pushed screen has asked for, by key. Held here, like History's record
    /// chains, so a span switched back to does not read again.
    private(set) var trends: [MetricTrendKey: LoadState<MetricTrend>] = [:]

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
        selection = .today
        state = .loading
        quickLog = .idle
        trends = [:]
    }

    /// Shows another day. The previous day's reading is dropped rather than kept as stale: a stale
    /// value is the same day not yet refreshed, and one day's recovery under another day's title
    /// would be wrong rather than old.
    func show(_ selection: DaySelection) async {
        guard selection != self.selection else { return }
        self.selection = selection
        state = .loading
        await refresh()
    }

    /// Today from the store's own clock, so the view and the reads agree on it.
    var today: CalendarDay { CalendarDay(now()) }

    private func reload() async {
        let moment = now()
        let today = CalendarDay(moment)
        let asked = selection
        let day = asked.day(today: today)

        // Today reads the wide window that answers "last" and "next" too; a past day reads only
        // the days around it, and the snapshot keeps the sessions dated on it.
        let sessionWindow = day == today
            ? (moment.addingTimeInterval(-Self.lookBack), moment.addingTimeInterval(Self.lookAhead))
            : (day.adding(days: -1).startDate(), day.adding(days: 2).startDate())
        let firstOuraDay = day.adding(days: -Self.ouraLookBackDays)

        do {
            async let window = sessions.sessions(from: sessionWindow.0, to: sessionWindow.1)
            async let ouraDays = oura.days(from: firstOuraDay, to: day)
            // A night beginning before midnight is filed under the waking day, so the event window
            // starts a day before the first day read and ends at the end of the day shown.
            async let nights = oura.sleepPeriods(
                from: firstOuraDay.adding(days: -1).startDate(), to: day.adding(days: 1).startDate())

            let snapshot = TodaySnapshot.reading(
                sessions: try await window,
                ouraDays: try await ouraDays,
                nights: try await nights,
                today: today,
                day: day)
            // A day changed while this was out has already reset the state; this answer is for the
            // old day and must not land under the new one.
            guard asked == selection else { return }
            state = state.applying(.success(snapshot))
        } catch is CancellationError {
            // The screen went away or a newer refresh took over. Nothing to say, and in particular
            // nothing to put on screen: the value already there is still the best one.
            return
        } catch {
            guard asked == selection else { return }
            state = state.applying(.failure((try? LoadFailure.from(error)) ?? .unavailable(
                String(describing: error))))
        }
    }

    // MARK: - Trends

    /// Reads one figure's trend. A failed re-read keeps the trend already shown.
    func loadTrend(_ key: MetricTrendKey) async {
        let range = key.range()
        let outcome: Result<MetricTrend, LoadFailure>
        do {
            async let days = oura.days(from: range.from, to: range.to)
            // Only the night figures need the nights, and a year of them is the heaviest read here.
            let nights = key.metric.isReadFromTheNight
                ? try await oura.sleepPeriods(
                    from: range.from.adding(days: -1).startDate(), to: range.to.adding(days: 1).startDate())
                : []
            outcome = .success(MetricTrend.reading(key, days: try await days, nights: nights))
        } catch is CancellationError {
            return
        } catch {
            outcome = .failure((try? LoadFailure.from(error)) ?? .unavailable(String(describing: error)))
        }
        trends[key] = (trends[key] ?? .loading).applying(outcome)
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
