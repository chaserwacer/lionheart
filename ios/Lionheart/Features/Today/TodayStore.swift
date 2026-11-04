import Foundation
import Observation

/// The Today screen's data, for whichever day it shows.
///
/// Three requests, in parallel — sessions, Oura days, Oura nights. They are independent, there is a
/// known number of them, and the screen needs all three — which is exactly what `async let` is for,
/// and what `APIClient` being `Sendable` exists to allow. On today one window of sessions answers
/// three of the screen's questions, so a wider single read beats three narrow ones and guarantees
/// the cards agree with each other.
///
/// Everything the screen decides is a pure function in `Core/Derived/`; this type does IO and turns
/// the outcome into one state case at one `do`/`catch`.
///
/// **A screen seen before opens on what it last showed.** A first load reads the same three parts
/// from the device before asking the server, so a cold launch paints the last answer at once and
/// the server's replaces it in place — rather than ten seconds of placeholders while a Mac asleep
/// behind Tailscale fails to answer. Coming back to the tab reads again only when `ReadFreshness`
/// says the answer may have moved.
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
    private let writes: WriteLedger
    private let now: @Sendable () -> Date

    private var load: Task<Void, Never>?
    /// The figures' trends read ahead of a tap, held so a second prefetch does not start beside it.
    private var trendPrefetch: Task<Void, Never>?
    /// When the day on screen was last read current from the server.
    private var lastRead: ReadStamp?
    /// The reading of today the held trends were read ahead for.
    private var trendsReadAheadFor: TodaySnapshot?
    private var whenReadToday: (@MainActor (TodaySnapshot) -> Void)?

    /// How long a current answer is taken as current when nothing on this device has written
    /// since. Short, because this is the screen opened first thing, and a morning sync on the web
    /// app or a run imported elsewhere should not wait long to show.
    private static let freshFor: TimeInterval = 5 * 60

    init(
        sessions: TrainingSessionsAPI,
        oura: OuraAPI,
        interpretation: InterpretationAPI,
        writes: WriteLedger = WriteLedger(),
        now: @escaping @Sendable () -> Date = { Date() }
    ) {
        self.sessions = sessions
        self.oura = oura
        self.interpretation = interpretation
        self.writes = writes
        self.now = now
    }

    /// Runs with every current reading of today — what feeds the widgets, which show today however
    /// the screen is being browsed.
    func onReadToday(_ handler: @escaping @MainActor (TodaySnapshot) -> Void) {
        whenReadToday = handler
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
        if load == task { load = nil }
    }

    /// Loads when the screen may be behind, and otherwise does nothing — what the tab coming back
    /// into view, or the app coming back to the foreground, asks for.
    func refreshIfDue() async {
        guard load == nil, ReadFreshness.needsRead(
            state, lastRead: lastRead, now: now(), acceptedWrites: writes.accepted, freshFor: Self.freshFor)
        else { return }
        await refresh()
    }

    /// Empties the screen. Registered with `AuthStore`, so signing out cannot leave one athlete's
    /// sessions on screen for the next one.
    func reset() {
        load?.cancel()
        load = nil
        trendPrefetch?.cancel()
        trendPrefetch = nil
        trendsReadAheadFor = nil
        lastRead = nil
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
        lastRead = nil
        await refresh()
    }

    /// Today from the store's own clock, so the view and the reads agree on it.
    var today: CalendarDay { CalendarDay(now()) }

    /// Paints the day from the device when there is nothing on screen, then asks the server.
    private func reload() async {
        let moment = now()
        let today = CalendarDay(moment)
        let asked = selection
        let day = asked.day(today: today)
        let writesBefore = writes.accepted

        if state.value == nil,
           let recalled = await APIClient.onDevice({ try await self.read(day, today: today, at: moment) }),
           let asOf = recalled.asOf,
           asked == selection {
            state = state.recalling(recalled.value, asOf: asOf)
        }

        do {
            let reading = try await read(day, today: today, at: moment)
            // A day changed while this was out has already reset the state; this answer is for the
            // old day and must not land under the new one.
            guard asked == selection else { return }
            state = state.applying(reading)
            guard reading.asOf == nil else { return }
            lastRead = ReadStamp(readAt: moment, acceptedWrites: writesBefore)
            guard day == today else { return }
            whenReadToday?(reading.value)
            prefetchTrends(for: reading.value)
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

    /// The three parts of one day's screen, read together. Answered from the device instead when
    /// run inside `APIClient.onDevice`.
    private func read(
        _ day: CalendarDay, today: CalendarDay, at moment: Date
    ) async throws -> Remembered<TodaySnapshot> {
        // Today reads the wide window that answers "last" and "next" too; a past day reads only
        // the days around it, and the snapshot keeps the sessions dated on it.
        let sessionWindow = day == today
            ? (moment.addingTimeInterval(-Self.lookBack), moment.addingTimeInterval(Self.lookAhead))
            : (day.adding(days: -1).startDate(), day.adding(days: 2).startDate())
        let firstOuraDay = day.adding(days: -Self.ouraLookBackDays)

        async let window = sessions.sessions(
            from: sessionWindow.0, to: sessionWindow.1,
            remembering: .today("sessions", showing: day, today: today))
        async let ouraDays = oura.days(
            from: firstOuraDay, to: day, remembering: .today("oura-days", showing: day, today: today))
        // A night beginning before midnight is filed under the waking day, so the event window
        // starts a day before the first day read and ends at the end of the day shown.
        async let nights = oura.sleepPeriods(
            from: firstOuraDay.adding(days: -1).startDate(), to: day.adding(days: 1).startDate(),
            remembering: .today("nights", showing: day, today: today))

        let (sessionsRead, ouraRead, nightsRead) = try await (window, ouraDays, nights)
        let snapshot = TodaySnapshot.reading(
            sessions: sessionsRead.value,
            ouraDays: ouraRead.value,
            nights: nightsRead.value,
            today: today,
            day: day)
        return .derived(snapshot, from: [sessionsRead.asOf, ouraRead.asOf, nightsRead.asOf])
    }

    // MARK: - Trends

    /// Reads one figure's trend. A failed re-read keeps the trend already shown, and with no
    /// server the last one read for the span is shown, marked with its time.
    ///
    /// The read answers every figure of its kind over the span, so every one of them is kept: a
    /// second figure opened over the same span is then on screen without a request.
    func loadTrend(_ key: MetricTrendKey) async {
        await loadTrends(
            over: key.span, endingOn: key.endingOn, withNights: key.metric.isReadFromTheNight, failing: key)
    }

    /// Reads the span every figure's page opens on, ahead of the tap that opens one, at a low
    /// priority: the figures on Today are the screen's most-opened drill-downs, and the read behind
    /// all of them is one request each for the days and the nights.
    ///
    /// Once per reading of today rather than once per day: a night's measurements that land after
    /// the morning's first read change the trends as well as the figures, and a read that brings
    /// nothing new reads nothing ahead.
    private func prefetchTrends(for snapshot: TodaySnapshot) {
        guard trendPrefetch == nil, snapshot != trendsReadAheadFor else { return }
        trendsReadAheadFor = snapshot
        let day = snapshot.recovery?.day ?? snapshot.day
        trendPrefetch = Task(priority: .utility) { [weak self] in
            await self?.loadTrends(over: .quarter, endingOn: day, withNights: true, failing: nil)
            self?.trendPrefetch = nil
        }
    }

    /// One read of a span's days, and its nights when asked, into the trend of every figure it
    /// answers. A failure lands only on `failing`, the trend a screen is waiting for; a prefetch
    /// that fails leaves nothing behind, and the page reads for itself when it opens.
    private func loadTrends(
        over span: TrendSpan, endingOn: CalendarDay, withNights: Bool, failing asked: MetricTrendKey?
    ) async {
        let range = MetricTrendKey(metric: .readiness, span: span, endingOn: endingOn).range()
        let today = self.today
        let name = "trend-\(span.days)"
        do {
            async let days = oura.days(
                from: range.from, to: range.to,
                remembering: .today("\(name).days", showing: endingOn, today: today))
            // Only the night figures need the nights, and a year of them is the heaviest read here.
            let nights: Remembered<[OuraEvent<OuraSleepPeriod>]> = withNights
                ? try await oura.sleepPeriods(
                    from: range.from.adding(days: -1).startDate(), to: range.to.adding(days: 1).startDate(),
                    remembering: .today("\(name).nights", showing: endingOn, today: today))
                : .current([])
            let daysRead = try await days
            for metric in RecoveryMetric.allCases where withNights || !metric.isReadFromTheNight {
                let key = MetricTrendKey(metric: metric, span: span, endingOn: endingOn)
                let trend = MetricTrend.reading(key, days: daysRead.value, nights: nights.value, today: today)
                trends[key] = (trends[key] ?? .loading).applying(
                    .derived(trend, from: [daysRead.asOf, nights.asOf]))
            }
        } catch is CancellationError {
            return
        } catch {
            guard let asked else { return }
            trends[asked] = (trends[asked] ?? .loading).applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
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
