import Foundation
import Observation

/// The movement screens' reads: one habits report per span, and any day's timeline opened.
@Observable
@MainActor
final class HabitsStore {
    private(set) var reports: [TrendSpan: LoadState<HabitsReport>] = [:]
    /// A day with no activity stored loads as `nil` — a fact about the day, not a failure.
    private(set) var days: [CalendarDay: LoadState<HabitsDayDetail?>] = [:]

    private let habits: HabitsAPI
    private let now: @Sendable () -> Date

    init(habits: HabitsAPI, now: @escaping @Sendable () -> Date = { Date() }) {
        self.habits = habits
        self.now = now
    }

    var today: CalendarDay { CalendarDay(now()) }

    func load(_ span: TrendSpan) async {
        let today = self.today
        let range = DayRange(from: today.adding(days: 1 - span.days), to: today)
        do {
            let read = try await habits.report(range, remembering: .habits(days: span.days))
            reports[span] = (reports[span] ?? .loading).applying(read)
        } catch is CancellationError {
            return
        } catch {
            reports[span] = (reports[span] ?? .loading).applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }

    func loadDay(_ day: CalendarDay) async {
        do {
            let read = try await habits.day(day, remembering: .habitsDay(day))
            days[day] = (days[day] ?? .loading).applying(read)
        } catch is CancellationError {
            return
        } catch {
            days[day] = (days[day] ?? .loading).applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }

    /// Reads a day only when no current one is held — what a screen showing the day asks each time
    /// it appears. A held day changes only when Oura's data does, and a sync reads every held day
    /// again itself (`refreshLoaded`).
    func loadDayIfNeeded(_ day: CalendarDay) async {
        if let held = days[day], held.value != nil, !held.isStale { return }
        await loadDay(day)
    }

    /// Whether any span or day on screen is what the device last knew rather than the server's answer.
    var isWaitingOnServer: Bool {
        reports.values.contains(where: \.isWaitingOnServer) || days.values.contains(where: \.isWaitingOnServer)
    }

    /// Reads again every span and day already read — after a sync has stored new days, or once the
    /// server answers again.
    func refreshLoaded() async {
        for span in reports.keys { await load(span) }
        for day in days.keys { await loadDay(day) }
    }

    func reset() {
        reports = [:]
        days = [:]
    }
}
