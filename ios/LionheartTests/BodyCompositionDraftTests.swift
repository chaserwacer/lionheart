import Foundation
import Testing

@testable import Lionheart

/// The review form a scan passes through, the arithmetic it checks, and what the scans say across
/// time.
@Suite("Body-composition review and history")
struct BodyCompositionDraftTests {
    // MARK: - The review form

    @Test("A reading fills the form with what was read and switches on every panel it read from")
    func aReadingFillsTheForm() throws {
        let draft = ScanDraft.reading(InBodySheetReader.read(try InBodySheetFixture.lines()))

        #expect(draft.entries[.skeletalMuscleMass] == "88.2")
        #expect(draft.entries[.height] == nil)
        #expect(draft.panels == Set(SheetPanel.allCases))
    }

    @Test("A form with figures still empty is no sheet, and says which")
    func emptyFiguresAreProblems() throws {
        var draft = ScanDraft.reading(InBodySheetReader.read(try InBodySheetFixture.lines()))
        draft.day = CalendarDay(year: 2026, month: 9, day: 14)
        draft.time = ClockTime(hour: 7, minute: 41)

        #expect(draft.scan == nil)
        #expect(Set(draft.problems) == [.missing(.height), .missing(.fatPercent(.leftArm))])
    }

    @Test("Filled from the paper, the form is the sheet, with the struck impedance rows left out")
    func aCompleteFormIsTheSheet() throws {
        var draft = ScanDraft.reading(InBodySheetReader.read(try InBodySheetFixture.lines()))
        draft.day = CalendarDay(year: 2026, month: 9, day: 14)
        draft.time = ClockTime(hour: 7, minute: 41)
        draft.entries[.height] = "71"
        draft.entries[.fatPercent(.leftArm)] = "90.1"

        let scan = try #require(draft.scan)

        #expect(scan.sheet.height == Height(value: 71, unit: .inches))
        #expect(scan.sheet.segmentalFat?.leftArm == SegmentReading(mass: 1.5, percentOfReference: 90.1))
        #expect(scan.sheet.impedance?.map(\.kilohertz) == [5])
        #expect(scan.sheet.sections.count == SheetPanel.allCases.count)
    }

    @Test("A panel switched off is sent as absent, whatever its boxes held")
    func aPanelSwitchedOffIsAbsent() throws {
        var draft = ScanDraft.correcting(AScan.inBody570())
        draft.panels.remove(.segmentalFat)

        let scan = try #require(draft.scan)

        #expect(scan.sheet.segmentalFat == nil)
        #expect(scan.sheet.segmentalLean != nil)
    }

    @Test("An impedance row half filled is a problem; an empty one is not")
    func partialImpedanceRow() {
        var draft = ScanDraft.correcting(AScan.inBody570())
        #expect(draft.problems.isEmpty)

        draft.entries[.impedance(kilohertz: 50, .trunk)] = "18.8"

        #expect(draft.problems == [.partialImpedanceRow(kilohertz: 50)])
    }

    @Test("A recorded scan reads back into the form and out again unchanged")
    func correctionRoundTrips() throws {
        let scan = AScan.inBody570()

        let restated = try #require(ScanDraft.correcting(scan).scan)

        #expect(restated.sheet == scan.sheet)
        #expect((restated.day, restated.time) == (scan.day, scan.time))
    }

    @Test("A figure typed into the wrong box is implausible")
    func implausibleFigure() {
        var draft = ScanDraft.correcting(AScan.inBody570())
        draft.entries[.extracellularWaterRatio] = "375"

        #expect(draft.problems == [.implausible(.extracellularWaterRatio)])
    }

    @Test("A new scan logs its weight; a correction keeps a day's reading that was not the scan's")
    func bodyweightAnswerByDefault() {
        let sheet = AScan.inBody570().sheet
        let scansOwn = BodyweightMeasurement(day: AScan.day, weight: 183.4, unit: .pounds)
        let morning = BodyweightMeasurement(day: AScan.day, weight: 181.0, unit: .pounds)

        #expect(ScanDraft.recordsBodyweight(correcting: nil, day: .logged(morning)))
        #expect(ScanDraft.recordsBodyweight(correcting: sheet, day: .nothing))
        #expect(ScanDraft.recordsBodyweight(correcting: sheet, day: .logged(scansOwn)))
        #expect(!ScanDraft.recordsBodyweight(correcting: sheet, day: .logged(morning)))
        #expect(DayBodyweight.logged(morning).differs(from: 183.4, .pounds) == morning)
        #expect(DayBodyweight.logged(scansOwn).differs(from: 183.4, .pounds) == nil)
    }

    // MARK: - The sheet's arithmetic

    @Test("A sheet whose figures add up raises nothing")
    func consistentSheet() {
        #expect(SheetCheck.disagreements(in: ScanDraft.correcting(AScan.inBody570())).isEmpty)
    }

    @Test("A BMI misread through a crease disagrees with weight and height, and says what they give")
    func misreadBodyMassIndex() throws {
        var draft = ScanDraft.correcting(AScan.inBody570())
        draft.entries[.bodyMassIndex] = "22.6"

        let check = try #require(SheetCheck.disagreements(in: draft).first)

        #expect(check.kind == .bodyMassIndex)
        #expect(abs(check.computed - 25.58) < 0.01)
    }

    @Test("BMI from kilograms and centimetres")
    func metricBodyMassIndex() throws {
        let index = try #require(SheetCheck.bodyMassIndex(weight: 80, unit: .kilograms, height: 180))

        #expect(abs(index - 24.69) < 0.01)
    }

    @Test("A check with one of its figures missing is not made")
    func incompleteCheckIsNotMade() {
        var draft = ScanDraft.correcting(AScan.inBody570())
        draft.entries[.height] = ""
        draft.entries[.bodyMassIndex] = "22.6"

        #expect(SheetCheck.disagreements(in: draft).isEmpty)
    }

    // MARK: - Across scans

    @Test("One scan has a point but no change")
    func oneScan() {
        let history = CompositionHistory([AScan.inBody570()])

        #expect(history.points(.skeletalMuscleMass).count == 1)
        #expect(history.change(.skeletalMuscleMass, at: AScan.inBody570()) == nil)
    }

    @Test("A scan is set against the one before it, oldest first whatever order they arrive in")
    func changeSinceThePrevious() throws {
        let earlier = AScan.inBody570(day: CalendarDay(year: 2026, month: 8, day: 25), muscle: 86.9)
        let latest = AScan.inBody570()
        let history = CompositionHistory([latest, earlier])

        let change = try #require(history.change(.skeletalMuscleMass, at: latest))

        #expect(history.latest == latest)
        #expect(abs(change - 1.3) < 0.0001)
    }

    @Test("A mass from a scan in the other unit is left off the line rather than converted")
    func unitsDoNotMix() {
        let kilograms = AScan.inBody570(day: CalendarDay(year: 2026, month: 8, day: 25), unit: .kilograms)
        let history = CompositionHistory([kilograms, AScan.inBody570()])

        #expect(history.points(.weight).map(\.day) == [AScan.day])
        #expect(history.points(.percentBodyFat).count == 2)
        #expect(history.change(.weight, at: AScan.inBody570()) == nil)
    }

    @Test("Segmental balance is left against right and the arm, trunk and leg pattern")
    func segmentalBalance() throws {
        let lean = try #require(AScan.inBody570().sheet.segmentalLean)

        let balance = SegmentalBalance.of(lean)

        #expect(abs(balance.armDifference - (9.02 - 9.10)) < 0.0001)
        #expect(abs(balance.legDifferencePercent - (22.71 - 22.90) / 22.90 * 100) < 0.0001)
        #expect(abs(balance.armsOfReference - 117.45) < 0.0001)
        #expect(balance.trunkOfReference == 112.2)
    }
}

/// Recorded scans for tests: the invented athlete's InBody 570 sheet that `InBodySheetFixture` and
/// the server's `ASheet` describe.
enum AScan {
    static let day = CalendarDay(year: 2026, month: 9, day: 14)

    static func inBody570(
        day: CalendarDay = day, muscle: Double = 88.2, unit: WeightUnit = .pounds
    ) -> BodyCompositionScan {
        BodyCompositionScan(
            id: UUID(uuidString: "00000000-0000-0000-0000-0000000000\(day.day)") ?? UUID(),
            day: day,
            time: ClockTime(hour: 7, minute: 41),
            sheet: BodyCompositionSheet(
                device: "InBody570", unit: unit, height: Height(value: 71, unit: .inches),
                weight: 183.4, skeletalMuscleMass: muscle, bodyFatMass: 30.2, leanBodyMass: 153.2,
                percentBodyFat: 16.5, bodyMassIndex: 25.6,
                sections: [
                    .bodyWater(.init(intracellular: 70.1, extracellular: 42.1, total: 112.2)),
                    .dryLeanMass(41.0),
                    .segmentalLean(Segments(
                        rightArm: .init(mass: 9.10, percentOfReference: 118.0),
                        leftArm: .init(mass: 9.02, percentOfReference: 116.9),
                        trunk: .init(mass: 68.4, percentOfReference: 112.2),
                        rightLeg: .init(mass: 22.90, percentOfReference: 106.5),
                        leftLeg: .init(mass: 22.71, percentOfReference: 105.6))),
                    .segmentalFat(Segments(
                        rightArm: .init(mass: 1.4, percentOfReference: 88.0),
                        leftArm: .init(mass: 1.5, percentOfReference: 90.1),
                        trunk: .init(mass: 15.8, percentOfReference: 160.4),
                        rightLeg: .init(mass: 4.2, percentOfReference: 101.3),
                        leftLeg: .init(mass: 4.2, percentOfReference: 100.9))),
                    .extracellularWaterRatio(0.375),
                    .fitnessScore(84),
                    .massControl(.init(bodyFat: -3.1, leanBody: 0)),
                    .basalMetabolicRate(1890),
                    .visceralFatLevel(6),
                    .impedance([
                        ImpedanceRow(kilohertz: 5, ohms: Segments(rightArm: 290.1, leftArm: 292.4, trunk: 21.9, rightLeg: 262.0, leftLeg: 263.3)),
                        ImpedanceRow(kilohertz: 500, ohms: Segments(rightArm: 214.4, leftArm: 216.0, trunk: 13.5, rightLeg: 197.3, leftLeg: 198.1)),
                    ]),
                ]))
    }
}
