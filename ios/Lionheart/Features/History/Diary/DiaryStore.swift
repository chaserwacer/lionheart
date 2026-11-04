import Foundation
import Observation

/// What History's day and week pages read beyond the log: the ring's days and nights, the cardio
/// report and the athlete's wellness ratings, for the day or week opened.
///
/// Four reads in parallel per page (`async let`), held by the day or the week so going back to one
/// does not read again, each with its own load state so a failed week does not blank a day already
/// shown. The lifting on both pages is the log's, which `HistoryStore` holds whole.
@Observable
@MainActor
final class DiaryStore {
    private(set) var days: [CalendarDay: LoadState<DiaryDay>] = [:]
    /// By Monday.
    private(set) var weeks: [CalendarDay: LoadState<DiaryWeek>] = [:]

    private let oura: OuraAPI
    private let cardio: CardioAPI
    private let wellness: WellnessAPI

    init(oura: OuraAPI, cardio: CardioAPI, wellness: WellnessAPI) {
        self.oura = oura
        self.cardio = cardio
        self.wellness = wellness
    }

    func loadDay(_ day: CalendarDay) async {
        let key = "diary.day.\(day.wire)"
        let range = DayRange(from: day, to: day)
        do {
            async let ouraDays = oura.days(from: day, to: day, remembering: ReadKey("\(key).oura-days"))
            // A night beginning before midnight is filed under the waking day.
            async let nights = oura.sleepPeriods(
                from: day.adding(days: -1).startDate(), to: day.adding(days: 1).startDate(),
                remembering: ReadKey("\(key).nights"))
            async let report = cardio.report(range, remembering: ReadKey("\(key).cardio"))
            async let ratings = wellness.states(from: day, to: day, remembering: ReadKey("\(key).wellness"))
            let (daysRead, nightsRead, reportRead, ratingsRead) = try await (ouraDays, nights, report, ratings)
            let reading = DiaryDay.reading(
                day: day, ouraDays: daysRead.value, nights: nightsRead.value,
                cardio: reportRead.value, wellness: ratingsRead.value)
            days[day] = (days[day] ?? .loading).applying(.derived(
                reading, from: [daysRead.asOf, nightsRead.asOf, reportRead.asOf, ratingsRead.asOf]))
        } catch is CancellationError {
            return
        } catch {
            days[day] = (days[day] ?? .loading).applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }

    /// The week starting on Monday `start`.
    func loadWeek(_ start: CalendarDay) async {
        let key = "diary.week.\(start.wire)"
        let end = start.adding(days: 6)
        do {
            async let ouraDays = oura.days(from: start, to: end, remembering: ReadKey("\(key).oura-days"))
            async let nights = oura.sleepPeriods(
                from: start.adding(days: -1).startDate(), to: end.adding(days: 1).startDate(),
                remembering: ReadKey("\(key).nights"))
            async let report = cardio.report(DayRange(from: start, to: end), remembering: ReadKey("\(key).cardio"))
            async let ratings = wellness.states(from: start, to: end, remembering: ReadKey("\(key).wellness"))
            let (daysRead, nightsRead, reportRead, ratingsRead) = try await (ouraDays, nights, report, ratings)
            let reading = DiaryWeek.reading(
                weekStarting: start, ouraDays: daysRead.value, nights: nightsRead.value,
                cardio: reportRead.value, wellness: ratingsRead.value)
            weeks[start] = (weeks[start] ?? .loading).applying(.derived(
                reading, from: [daysRead.asOf, nightsRead.asOf, reportRead.asOf, ratingsRead.asOf]))
        } catch is CancellationError {
            return
        } catch {
            weeks[start] = (weeks[start] ?? .loading).applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }

    /// A cardio session on a day or in a week already read, for its heart-rate screen.
    func cardioSession(_ id: String, on day: CalendarDay) -> (CardioSession, MaxHeartRate?)? {
        if let read = days[day]?.value, let session = read.cardio.first(where: { $0.id == id }) {
            return (session, read.maxHeartRate)
        }
        let week = TrainingTrends.weekStart(of: day) ?? day
        if let read = weeks[week]?.value, let session = read.cardioSessions.first(where: { $0.id == id }) {
            return (session, read.maxHeartRate)
        }
        return nil
    }

    /// Reads again the days and weeks already read that reach `first` or later — what a sync can
    /// have changed, since it pulls only the newest days. Older pages keep what they read, so a
    /// session spent stepping back through months does not cost a request per page per sync.
    func refreshLoaded(since first: CalendarDay) async {
        for day in days.keys where day >= first { await loadDay(day) }
        for week in weeks.keys where week.adding(days: 6) >= first { await loadWeek(week) }
    }

    /// Reads again the one day and the one week a wellness rating of `day` shows on, where read.
    func refresh(around day: CalendarDay) async {
        if days[day] != nil { await loadDay(day) }
        if let week = TrainingTrends.weekStart(of: day), weeks[week] != nil { await loadWeek(week) }
    }

    func reset() {
        days = [:]
        weeks = [:]
    }
}
