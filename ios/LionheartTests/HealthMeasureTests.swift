import Foundation
import Synchronization
import Testing

@testable import Lionheart

/// What Apple's devices measured: which readings are theirs, how readings become days, which days a
/// sync reads, how the figures are compared and written, the wire, and the sync itself. Every value
/// is invented.
@Suite("Apple Health measurements")
struct HealthMeasureTests {
    private static let utc: Calendar = {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "UTC")!
        return calendar
    }()

    private static let watch = ReadingSource(bundleIdentifier: "com.apple.health.A1B2C3D4-0000-4000-8000-000000000001", productType: "Watch6,2")
    private static let phone = ReadingSource(bundleIdentifier: "com.apple.health.A1B2C3D4-0000-4000-8000-000000000002", productType: "iPhone14,5")

    // MARK: - Whose readings

    @Test("A reading is the watch's or the phone's only when Apple's own device measured it")
    func onlyApplesDevicesCount() {
        #expect(MeasuringDevice.measuring(Self.watch) == .appleWatch)
        #expect(MeasuringDevice.measuring(Self.phone) == .iPhone)
        // Oura's app writes from the iPhone it runs on, under its own bundle.
        #expect(MeasuringDevice.measuring(ReadingSource(bundleIdentifier: "com.ouraring.oura", productType: "iPhone14,5")) == nil)
        // Typed into the Health app by hand.
        #expect(MeasuringDevice.measuring(ReadingSource(bundleIdentifier: "com.apple.Health", productType: "iPhone14,5")) == nil)
        #expect(MeasuringDevice.measuring(ReadingSource(bundleIdentifier: "com.apple.health.X", productType: nil)) == nil)
    }

    @Test("Readings become one day per figure and device, with mean and extremes; another app's are counted out")
    func readingsBecomeDays() {
        let morning = Self.instant("2022-04-11T07:00:00Z")
        let evening = Self.instant("2022-04-11T21:00:00Z")
        let reduced = DailyMeasures.reduced([
            HealthReading(metric: .heartRateVariabilitySdnn, at: morning, value: 40, source: Self.watch),
            HealthReading(metric: .heartRateVariabilitySdnn, at: evening, value: 60, source: Self.watch),
            HealthReading(metric: .walkingSpeed, at: morning, value: 1.3, source: Self.phone),
            HealthReading(metric: .walkingSpeed, at: evening, value: 1.4, source: Self.watch),
            HealthReading(metric: .heartRateVariabilitySdnn, at: morning, value: 25,
                          source: ReadingSource(bundleIdentifier: "com.ouraring.oura", productType: "iPhone14,5")),
        ], in: Self.utc)

        let day = CalendarDay(year: 2022, month: 4, day: 11)
        #expect(reduced.fromOtherApps == 1)
        #expect(reduced.days.count == 3)
        #expect(reduced.days.first { $0.metric == .heartRateVariabilitySdnn }?.measured
                == MeasuredDay(day: day, mean: 50, minimum: 40, maximum: 60, count: 2))
        #expect(Set(reduced.days.filter { $0.metric == .walkingSpeed }.map(\.device)) == [.iPhone, .appleWatch])
    }

    @Test("A weigh-in falls on the calendar day it was taken in the phone's zone")
    func aWeighInHasItsLocalDay() {
        var eastern = Calendar(identifier: .gregorian)
        eastern.timeZone = TimeZone(identifier: "America/New_York")!

        let weighIn = WeighIn(BodyMassReading(at: Self.instant("2022-04-12T02:00:00Z"), kilograms: 80), in: eastern)

        #expect(weighIn.day == CalendarDay(year: 2022, month: 4, day: 11))
    }

    // MARK: - Which days a sync reads

    @Test("A first sync reads from Health's oldest reading to today a quarter at a time")
    func aFirstSyncReadsEverything() {
        let today = CalendarDay(year: 2022, month: 12, day: 31)
        let windows = HealthMeasureSyncPlan.windows(
            earliest: CalendarDay(year: 2022, month: 6, day: 1), mark: .start, today: today)

        #expect(windows.first?.from == CalendarDay(year: 2022, month: 6, day: 1))
        #expect(windows.last?.to == today)
        #expect(windows.count == 3)
        #expect(zip(windows, windows.dropFirst()).allSatisfy { $0.to.adding(days: 1) == $1.from })
    }

    @Test("A later sync reads again from a week before the last day it finished")
    func aLaterSyncRereadsAWeek() {
        let today = CalendarDay(year: 2026, month: 11, day: 4)
        let mark = HealthMeasureMark(syncedThrough: CalendarDay(year: 2026, month: 11, day: 3), lastSynced: nil)

        let windows = HealthMeasureSyncPlan.windows(earliest: nil, mark: mark, today: today)

        #expect(windows == [DayRange(from: CalendarDay(year: 2026, month: 10, day: 27), to: today)])
    }

    @Test("With nothing in Health, a sync reads today alone")
    func anEmptyHealthReadsToday() {
        let today = CalendarDay(year: 2026, month: 11, day: 4)
        #expect(HealthMeasureSyncPlan.windows(earliest: nil, mark: .start, today: today) == [DayRange(from: today, to: today)])
    }

    // MARK: - Reading the figures

    @Test("The last four weeks are set against the twelve before them")
    func theComparisonWindows() {
        let end = CalendarDay(year: 2026, month: 11, day: 4)
        let days = [
            Self.day(end, mean: 0.05),
            Self.day(end.adding(days: -27), mean: 0.03),
            Self.day(end.adding(days: -28), mean: 0.02),
            Self.day(end.adding(days: -111), mean: 0.04),
            Self.day(end.adding(days: -112), mean: 0.90),
        ]

        let comparison = MeasureComparison.of(days, endingOn: end)

        #expect(Self.close(comparison?.recent, 0.04))
        #expect(Self.close(comparison?.before, 0.03))
        #expect(comparison?.recentDays == 2)
        #expect(MeasureComparison.of(days, endingOn: end.adding(days: 400)) == nil)
    }

    @Test("Weeks start on Monday and hold the mean of their days")
    func weeksStartOnMonday() {
        // 4 November 2026 is a Wednesday.
        let wednesday = CalendarDay(year: 2026, month: 11, day: 4)
        let weeks = WeekMean.of([Self.day(wednesday, mean: 1.2), Self.day(wednesday.adding(days: 4), mean: 1.4)])

        #expect(weeks.map(\.weekStarting) == [CalendarDay(year: 2026, month: 11, day: 2)])
        #expect(Self.close(weeks.first?.mean, 1.3))
        #expect(weeks.first?.days == 2)
    }

    @Test("An injury's gait sets the eight weeks before it against the four from it, and the last four once it is older")
    func injuryGaitWindows() {
        let injury = CalendarDay(year: 2026, month: 6, day: 1)
        let today = CalendarDay(year: 2026, month: 11, day: 4)
        let days = [
            Self.day(injury.adding(days: -56), mean: 0.02),
            Self.day(injury.adding(days: -1), mean: 0.04),
            Self.day(injury, mean: 0.10),
            Self.day(injury.adding(days: 27), mean: 0.08),
            Self.day(injury.adding(days: 28), mean: 0.50),
            Self.day(today, mean: 0.03),
        ]

        let gait = InjuryGait.of(.walkingAsymmetry, days, injuredOn: injury, today: today)

        #expect(Self.close(gait.before, 0.03))
        #expect(Self.close(gait.after, 0.09))
        #expect(Self.close(gait.recent, 0.03))
        #expect(Self.close(gait.change, 0.06))
        #expect(gait.around.count == 5)
    }

    @Test("A recent injury has no separate last four weeks")
    func aRecentInjuryHasNoRecentWindow() {
        let injury = CalendarDay(year: 2026, month: 10, day: 20)
        let gait = InjuryGait.of(
            .walkingDoubleSupport, [Self.day(injury, mean: 0.30)], injuredOn: injury, today: injury.adding(days: 10))

        #expect(gait.recent == nil)
        #expect(gait.after == 0.30)
    }

    @Test("Shares read as percentages and step length in centimetres; a change is signed and judged by the figure's direction")
    func figuresAreWrittenInTheirShownUnit() {
        #expect(HealthMetric.walkingAsymmetry.text(0.042) == "4.2%")
        #expect(HealthMetric.walkingStepLength.text(0.72) == "72 cm")
        #expect(HealthMetric.walkingSpeed.text(1.315) == "1.32 m/s")
        #expect(HealthMetric.walkingAsymmetry.change(-0.011) == "−1.1")
        #expect(HealthMetric.walkingAsymmetry.isImprovement(-0.011) == true)
        #expect(HealthMetric.walkingSpeed.isImprovement(-0.05) == false)
        #expect(HealthMetric.restingHeartRate.isImprovement(0.2) == nil)
    }

    @Test("The principal series is the device that measured a figure on the most days")
    func thePrincipalSeries() {
        let day = CalendarDay(year: 2026, month: 1, day: 5)
        let history = HealthMeasureHistory(series: [
            HealthMeasureSeries(metric: .walkingSpeed, device: .appleWatch, days: [Self.day(day, mean: 1.4)]),
            HealthMeasureSeries(metric: .walkingSpeed, device: .iPhone, days: [Self.day(day, mean: 1.3), Self.day(day.adding(days: 1), mean: 1.2)]),
        ])

        #expect(history.principal(.walkingSpeed)?.device == .iPhone)
        #expect(history.principal(.vo2Max) == nil)
    }

    // MARK: - The wire

    @Test("A series decodes with its days; one naming a figure this build does not know is dropped")
    func seriesDecode() throws {
        let series = try Decoding.list(HealthMeasureSeries.self, """
        [
          {"metric": "walkingAsymmetry", "device": "iPhone",
           "days": [{"day": "2026-01-05", "mean": 0.03, "minimum": 0.01, "maximum": 0.06, "count": 9}]},
          {"metric": "somethingNew", "device": "iPhone", "days": []}
        ]
        """)

        #expect(series == [HealthMeasureSeries(
            metric: .walkingAsymmetry, device: .iPhone,
            days: [MeasuredDay(day: CalendarDay(year: 2026, month: 1, day: 5), mean: 0.03, minimum: 0.01, maximum: 0.06, count: 9)])])
    }

    @Test("A day offered goes out flat, in the server's spelling")
    func anOfferEncodes() throws {
        let offer = MeasuredDayOffer(
            metric: .heartRateVariabilitySdnn, device: .appleWatch,
            measured: MeasuredDay(day: CalendarDay(year: 2021, month: 3, day: 14), mean: 48, minimum: 22, maximum: 91, count: 7))

        let json = try JSONSerialization.jsonObject(with: Wire.encoder().encode(offer)) as? [String: Any]

        #expect(json?["metric"] as? String == "heartRateVariabilitySdnn")
        #expect(json?["device"] as? String == "appleWatch")
        #expect(json?["day"] as? String == "2021-03-14")
        #expect(json?["count"] as? Int == 7)
    }

    @Test("The cardio report carries the watch's VO₂max as its own series")
    func theCardioReportCarriesTheWatchsVo2Max() throws {
        let report = try Decoding.decode(CardioReport.self, """
        {"range": {"from": "2022-03-01", "to": "2022-05-31"},
         "vo2Max": [], "appleWatchVo2Max": [{"day": "2022-04-02", "value": 51.3}]}
        """)

        #expect(report.vo2Max.isEmpty)
        #expect(report.appleWatchVo2Max == [DayFigure(day: CalendarDay(year: 2022, month: 4, day: 2), value: 51.3)])
    }

    // MARK: - The sync

    @Test("A sync offers each window's days and weigh-ins, keeps the mark, sets the age, and says what it did")
    @MainActor
    func aSyncOffersEverything() async {
        let now = Self.instant("2022-12-31T12:00:00Z")
        let health = AHealth(
            earliest: Self.instant("2022-06-01T12:00:00Z"),
            readings: [
                HealthReading(metric: .vo2Max, at: Self.instant("2022-06-02T12:00:00Z"), value: 50, source: Self.watch),
                HealthReading(metric: .walkingSpeed, at: Self.instant("2022-11-02T12:00:00Z"), value: 1.3, source: Self.phone),
            ],
            weighIns: [BodyMassReading(at: Self.instant("2022-07-02T07:00:00Z"), kilograms: 80)],
            born: CalendarDay(year: 1990, month: 5, day: 20))
        let server = AServer()
        let marks = Marks()
        let store = HealthMeasureSyncStore(health: health, server: server, marks: marks, now: { now }, calendar: Self.utc)
        var reread: [HealthMeasureTally] = []
        store.onSynced { reread.append($0) }

        await store.sync()

        #expect(health.windowsRead == 3)
        #expect(server.daysOffered.map(\.metric) == [.vo2Max, .walkingSpeed])
        #expect(server.weighInsOffered == 1)
        #expect(server.born == CalendarDay(year: 1990, month: 5, day: 20))
        #expect(marks.mark == HealthMeasureMark(syncedThrough: CalendarDay(year: 2022, month: 12, day: 31), lastSynced: now))
        guard case .synced(let tally, _) = store.state else {
            Issue.record("Expected a finished sync, got \(store.state)")
            return
        }
        #expect((tally.daysRecorded, tally.bodyweightsAdded, tally.setsReweighed) == (2, 1, 3))
        #expect(reread == [tally])
    }

    @Test("A sync stopped by the server keeps the mark of the last window it finished")
    @MainActor
    func aStoppedSyncResumes() async {
        let now = Self.instant("2022-12-31T12:00:00Z")
        let health = AHealth(
            earliest: Self.instant("2022-06-01T12:00:00Z"),
            readings: [
                HealthReading(metric: .vo2Max, at: Self.instant("2022-06-02T12:00:00Z"), value: 50, source: Self.watch),
                HealthReading(metric: .vo2Max, at: Self.instant("2022-10-02T12:00:00Z"), value: 51, source: Self.watch),
            ],
            weighIns: [], born: nil)
        let server = AServer(failOnOffer: 2)
        let marks = Marks()
        let store = HealthMeasureSyncStore(health: health, server: server, marks: marks, now: { now }, calendar: Self.utc)

        await store.sync()

        #expect(marks.mark.syncedThroughDay == CalendarDay(year: 2022, month: 8, day: 29))
        guard case .failed(_, let done, _) = store.state else {
            Issue.record("Expected a stopped sync, got \(store.state)")
            return
        }
        #expect(done.daysRecorded == 1)
    }

    @Test("Without Health connected a sync reads nothing")
    @MainActor
    func anUnconnectedSyncReadsNothing() async {
        let health = AHealth(earliest: nil, readings: [], weighIns: [], born: nil, needsAuthorization: true)
        let store = HealthMeasureSyncStore(health: health, server: AServer(), marks: Marks(), calendar: Self.utc)

        await store.sync()

        #expect(store.state == .notConnected)
        #expect(health.windowsRead == 0)
    }

    // MARK: - Helpers

    private static func instant(_ wire: String) -> Date { WireDate.instant(from: wire)! }

    private static func close(_ value: Double?, _ expected: Double) -> Bool {
        value.map { abs($0 - expected) < 1e-9 } ?? false
    }

    private static func day(_ day: CalendarDay, mean: Double) -> MeasuredDay {
        MeasuredDay(day: day, mean: mean, minimum: mean, maximum: mean, count: 1)
    }
}

private final class AHealth: HealthMeasureSource {
    private let earliest: Date?
    private let held: [HealthReading]
    private let heldWeighIns: [BodyMassReading]
    private let born: CalendarDay?
    private let needsAsking: Bool
    private let reads = Mutex(0)

    init(earliest: Date?, readings: [HealthReading], weighIns: [BodyMassReading], born: CalendarDay?, needsAuthorization: Bool = false) {
        self.earliest = earliest
        held = readings
        heldWeighIns = weighIns
        self.born = born
        needsAsking = needsAuthorization
    }

    var windowsRead: Int { reads.withLock { $0 } }

    var isAvailable: Bool { true }
    func needsAuthorization() async -> Bool { needsAsking }
    func requestAuthorization() async throws {}
    func earliestReading() async throws -> Date? { earliest }

    func readings(from: Date, to: Date) async throws -> [HealthReading] {
        reads.withLock { $0 += 1 }
        return held.filter { $0.at >= from && $0.at < to }
    }

    func weighIns(from: Date, to: Date) async throws -> [BodyMassReading] {
        heldWeighIns.filter { $0.at >= from && $0.at < to }
    }

    func dateOfBirth() async -> CalendarDay? { born }
}

private final class AServer: HealthMeasureImporting {
    private let state: Mutex<(days: [MeasuredDayOffer], weighIns: Int, born: CalendarDay?, offers: Int)>
    private let failOnOffer: Int?

    init(failOnOffer: Int? = nil) {
        state = Mutex(([], 0, nil, 0))
        self.failOnOffer = failOnOffer
    }

    var daysOffered: [MeasuredDayOffer] { state.withLock { $0.days } }
    var weighInsOffered: Int { state.withLock { $0.weighIns } }
    var born: CalendarDay? { state.withLock { $0.born } }

    func offer(_ days: [MeasuredDayOffer]) async throws -> HealthMeasuresImportReport {
        try state.withLock { held in
            held.offers += 1
            if held.offers == failOnOffer { throw APIError.transport("offline") }
            held.days += days
            return HealthMeasuresImportReport(recorded: days.count, restated: 0, unchanged: 0, unreadable: 0)
        }
    }

    func offer(_ weighIns: [WeighIn]) async throws -> HealthBodyweightImportReport {
        state.withLock { $0.weighIns += weighIns.count }
        return HealthBodyweightImportReport(added: weighIns.count, daysAlreadyMeasured: 0, unreadable: 0, setsReweighed: 3)
    }

    func apply(dateOfBirth: CalendarDay) async throws -> ProfileAge {
        state.withLock { $0.born = dateOfBirth }
        return ProfileAge(age: 36, changed: true)
    }
}

private final class Marks: HealthMeasureMarks {
    private let held = Mutex(HealthMeasureMark.start)

    var mark: HealthMeasureMark { held.withLock { $0 } }

    func read() -> HealthMeasureMark { mark }
    func write(_ mark: HealthMeasureMark) { held.withLock { $0 = mark } }
    func clear() { held.withLock { $0 = .start } }
}
