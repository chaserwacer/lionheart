import Foundation
import Observation

/// The athlete's wellness states over a span beside Oura's readiness, and the write that logs one.
///
/// Held per span, like the cardio and habits reports, so switching back does not read again. The
/// two reads are made together (`async let`) because the screen reads them against each other —
/// a rating beside a readiness from another moment would be no comparison.
@Observable
@MainActor
final class WellnessStore {
    private(set) var spans: [TrendSpan: LoadState<WellnessAgainstReadiness>] = [:]

    private let wellness: WellnessAPI
    private let oura: OuraAPI
    private let now: @Sendable () -> Date
    private var whenLogged: [@MainActor (WellnessEntry) async -> Void] = []

    init(wellness: WellnessAPI, oura: OuraAPI, now: @escaping @Sendable () -> Date = { Date() }) {
        self.wellness = wellness
        self.oura = oura
        self.now = now
    }

    var today: CalendarDay { CalendarDay(now()) }

    /// Runs after every entry the server takes, for the screens that show wellness beside other
    /// things — Body's count and the diary's days.
    func onLogged(_ action: @escaping @MainActor (WellnessEntry) async -> Void) {
        whenLogged.append(action)
    }

    /// The span's entries and readiness, ending today. A failed re-read keeps what is shown.
    func load(_ span: TrendSpan) async {
        let today = self.today
        let first = today.adding(days: 1 - span.days)
        do {
            async let entries = wellness.states(from: first, to: today, remembering: ReadKey("wellness.\(span.days).states"))
            async let days = oura.days(from: first, to: today, remembering: ReadKey("wellness.\(span.days).oura-days"))
            let (entriesRead, daysRead) = try await (entries, days)
            spans[span] = (spans[span] ?? .loading).applying(.derived(
                WellnessAgainstReadiness.of(entries: entriesRead.value, ouraDays: daysRead.value),
                from: [entriesRead.asOf, daysRead.asOf]))
        } catch is CancellationError {
            return
        } catch {
            spans[span] = (spans[span] ?? .loading).applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }

    /// Logs one day's ratings, then reads again every span on screen and tells the screens that
    /// show wellness elsewhere.
    ///
    /// Thrown rather than kept as state, for the reason `BodyStore.createInjury` throws: the form
    /// is a sheet, which stays up holding the ratings while it says why the server refused. A
    /// rating needs the server; an entry the device held back would be a second account of a day
    /// the server keeps one of.
    ///
    /// - Throws: `LoadFailure` when the server refuses or cannot be reached; `CancellationError`
    ///   when the sheet went away first.
    func log(_ entry: WellnessEntry) async throws {
        do {
            _ = try await wellness.add(entry)
        } catch {
            throw try LoadFailure.from(error)
        }
        for span in spans.keys { await load(span) }
        for action in whenLogged { await action(entry) }
    }

    func reset() {
        spans = [:]
    }
}
