import Foundation
import Observation

/// The cardio screen's reads: one report per span, and the heart rate across any session opened.
///
/// Held per span like Today's trends, so switching back to a span does not read again, and each
/// keeps its own load state so a failed year does not blank the quarter already on screen.
@Observable
@MainActor
final class CardioStore {
    private(set) var reports: [TrendSpan: LoadState<CardioReport>] = [:]
    private(set) var heartRate: [String: LoadState<[HeartRateSample]>] = [:]
    /// The cardio of one score window, by its last day: the sessions the score counted and the
    /// ring's guesses it declined.
    private(set) var windows: [CalendarDay: LoadState<CardioReport>] = [:]

    private let cardio: CardioAPI
    private let oura: OuraAPI
    private let now: @Sendable () -> Date

    init(cardio: CardioAPI, oura: OuraAPI, now: @escaping @Sendable () -> Date = { Date() }) {
        self.cardio = cardio
        self.oura = oura
        self.now = now
    }

    var today: CalendarDay { CalendarDay(now()) }

    /// The span's report, ending today. A failed re-read keeps the report already shown.
    func load(_ span: TrendSpan) async {
        let today = self.today
        let range = DayRange(from: today.adding(days: 1 - span.days), to: today)
        do {
            let read = try await cardio.report(range, remembering: .cardio(days: span.days))
            reports[span] = (reports[span] ?? .loading).applying(read)
        } catch is CancellationError {
            return
        } catch {
            reports[span] = (reports[span] ?? .loading).applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }

    /// The cardio report for one seven-day score window. A failed re-read keeps the report already shown.
    func load(window: DayRange) async {
        do {
            let read = try await cardio.report(window, remembering: ReadKey("cardio.window.\(window.to.wire)"))
            windows[window.to] = (windows[window.to] ?? .loading).applying(read)
        } catch is CancellationError {
            return
        } catch {
            windows[window.to] = (windows[window.to] ?? .loading).applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }

    /// The all-day series across one session, a little either side so the climb in and the
    /// recovery out are on the chart.
    func loadHeartRate(for session: CardioSession) async {
        let from = session.start.addingTimeInterval(-10 * 60)
        let to = session.end.addingTimeInterval(20 * 60)
        do {
            let read = try await oura.heartRate(from: from, to: to, remembering: .heartRate(from: from, to: to))
            heartRate[session.id] = (heartRate[session.id] ?? .loading).applying(read)
        } catch is CancellationError {
            return
        } catch {
            heartRate[session.id] = (heartRate[session.id] ?? .loading).applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }

    /// Whether any span on screen is what the device last knew rather than the server's answer.
    var isWaitingOnServer: Bool { reports.values.contains(where: \.isWaitingOnServer) }

    /// Reads again every span already read — after a sync has stored new days, or once the server
    /// answers again.
    func refreshLoaded() async {
        for span in reports.keys { await load(span) }
    }

    func reset() {
        reports = [:]
        heartRate = [:]
        windows = [:]
    }
}
