import Foundation
import Testing

@testable import Lionheart

/// Reading an InBody sheet's figures out of recognised text, and putting several readings together.
///
/// The claim throughout is the one the review form rests on: a figure is read where the sheet's
/// layout puts it, or not at all.
@Suite("Reading a body-composition sheet")
struct BodyCompositionReadingTests {
    // MARK: - One reading of a photographed sheet

    @Test("Every legible figure on the photographed sheet is read where the layout puts it")
    func readsTheLegibleFigures() throws {
        let reading = InBodySheetReader.read(try InBodySheetFixture.lines())

        var expected: [SheetField: Double] = [
            .weight: 183.4, .skeletalMuscleMass: 88.2, .bodyFatMass: 30.2, .leanBodyMass: 153.2,
            .percentBodyFat: 16.5, .bodyMassIndex: 25.6,
            .intracellularWater: 70.1, .extracellularWater: 42.1, .totalBodyWater: 112.2, .dryLeanMass: 41.0,
            .extracellularWaterRatio: 0.375, .fitnessScore: 84, .fatControl: -3.1, .leanControl: 0,
            .basalMetabolicRate: 1890, .visceralFatLevel: 6,
            .fatMass(.rightArm): 1.4, .fatMass(.leftArm): 1.5, .fatMass(.trunk): 15.8,
            .fatMass(.rightLeg): 4.2, .fatMass(.leftLeg): 4.2,
            .fatPercent(.rightArm): 88.0, .fatPercent(.trunk): 160.4,
            .fatPercent(.rightLeg): 101.3, .fatPercent(.leftLeg): 100.9,
        ]
        let lean: [(BodySegment, Double, Double)] = [
            (.rightArm, 9.10, 118.0), (.leftArm, 9.02, 116.9), (.trunk, 68.4, 112.2),
            (.rightLeg, 22.90, 106.5), (.leftLeg, 22.71, 105.6),
        ]
        for (segment, mass, percent) in lean {
            expected[.leanMass(segment)] = mass
            expected[.leanPercent(segment)] = percent
        }
        for (segment, ohms) in zip(BodySegment.allCases, [290.1, 292.4, 21.9, 262.0, 263.3]) {
            expected[.impedance(kilohertz: 5, segment)] = ohms
        }

        #expect(reading.values == expected)
        #expect(reading.device == "InBody570")
        #expect(reading.unit == .pounds)
    }

    @Test("A figure the photo shows two ways on one row is left unread rather than picked")
    func twoReadingsOnOneRowStayUnread() throws {
        let reading = InBodySheetReader.read(try InBodySheetFixture.lines())

        #expect(reading.values[.fatPercent(.leftArm)] == nil)
    }

    @Test("A faded height, test date and garbled time are left unread rather than guessed")
    func fadedHeaderStaysUnread() throws {
        let reading = InBodySheetReader.read(try InBodySheetFixture.lines())

        #expect(reading.values[.height] == nil)
        #expect(reading.day == nil)
        #expect(reading.time == nil)
    }

    @Test("A bar whose value is illegible stays empty rather than taking a scale label printed to the same places")
    func anIllegibleBarValueStaysEmpty() throws {
        let reading = InBodySheetReader.read(try InBodySheetFixture.lines().filter { $0.text != "25.6" })

        #expect(reading.values[.bodyMassIndex] == nil)
        #expect(reading.values[.percentBodyFat] == 16.5)
    }

    @Test("On a photo taken at an angle, a bar reads its value and never a scale label in its place", arguments: [-0.015, 0.015])
    func aTiltedPhotoKeepsItsBarsHonest(slope: Double) throws {
        let tilted = try InBodySheetFixture.lines().map { line in
            RecognizedLine(text: line.text, x: line.x, y: line.y + slope * line.x, width: line.width, height: line.height)
        }

        #expect(InBodySheetReader.read(tilted).values[.bodyMassIndex] == 25.6)
        #expect(InBodySheetReader.read(tilted.filter { $0.text != "25.6" }).values[.bodyMassIndex] == nil)
    }

    @Test("With one of the table's sum columns illegible, none of the sums is placed")
    func aMissingSumColumnLeavesTheSumsEmpty() throws {
        let reading = InBodySheetReader.read(try InBodySheetFixture.lines().filter { $0.text != "153,2" })

        #expect(reading.values[.leanBodyMass] == nil)
        #expect(reading.values[.totalBodyWater] == nil)
        #expect(reading.values[.weight] == 183.4)
        #expect(reading.values[.intracellularWater] == 70.1)
    }

    @Test("The ECW/TBW scale's labels are not read as the ratio")
    func theRatioScaleIsNotTheRatio() throws {
        let reading = InBodySheetReader.read(try InBodySheetFixture.lines().filter { $0.text != "0.375" })

        #expect(reading.values[.extracellularWaterRatio] == nil)
    }

    @Test("A legible header gives the height, the test date and the time")
    func legibleHeader() {
        let reading = InBodySheetReader.read([
            line("[InBody570]", x: 0.56, y: 0.08),
            line("Height 5ft 11.0in", x: 0.24, y: 0.10),
            line("Test Date / Time 2026.09.14. 07:41", x: 0.40, y: 0.10),
            line("Body Composition Analysis", x: 0.06, y: 0.14, height: 0.024),
        ])

        #expect(reading.values[.height] == 71)
        #expect(reading.day == CalendarDay(year: 2026, month: 9, day: 14))
        #expect(reading.time == ClockTime(hour: 7, minute: 41))
    }

    @Test("A date whose order is in doubt is not read")
    func ambiguousDateStaysUnread() {
        let reading = InBodySheetReader.read([
            line("Test Date / Time 10.11.2026 07:41", x: 0.40, y: 0.10),
            line("Body Composition Analysis", x: 0.06, y: 0.14, height: 0.024),
        ])

        #expect(reading.day == nil)
    }

    @Test("A fitness score that lost a digit to a crease is not read as a one-digit score")
    func aClippedScoreStaysUnread() {
        let reading = InBodySheetReader.read([
            line("Fitness Score", x: 0.68, y: 0.166, height: 0.010),
            line("8 //100 Points", x: 0.77, y: 0.19, height: 0.022),
            line("Body Fat - Lean Body Mass Control", x: 0.68, y: 0.25, height: 0.017),
        ])

        #expect(reading.values[.fitnessScore] == nil)
    }

    // MARK: - Several readings

    @Test("A majority gives the figure, and the minority's reading stays on record for the form to show")
    func majorityWins() {
        let merged = SheetReading.merging([
            reading([.leanMass(.leftLeg): 22.71, .weight: 183.4]),
            reading([.leanMass(.leftLeg): 22.21, .weight: 183.4]),
            reading([.leanMass(.leftLeg): 22.71]),
        ])

        #expect(merged.values == [.leanMass(.leftLeg): 22.71, .weight: 183.4])
        #expect(merged.disagreements == [.leanMass(.leftLeg): [22.71, 22.21]])
    }

    @Test("Readings that split evenly leave the figure unread, with what each saw")
    func aTieStaysUnread() {
        let merged = SheetReading.merging([
            reading([.fatPercent(.leftArm): 90.1]),
            reading([.fatPercent(.leftArm): 96.1]),
        ])

        #expect(merged.values[.fatPercent(.leftArm)] == nil)
        #expect(Set(merged.disagreements[.fatPercent(.leftArm)] ?? []) == [90.1, 96.1])
    }

    @Test("A figure only one reading found is taken")
    func aLoneReadingIsTaken() {
        let merged = SheetReading.merging([reading([.basalMetabolicRate: 1890]), reading([:])])

        #expect(merged.values == [.basalMetabolicRate: 1890])
    }

    // MARK: - Helpers

    private func line(_ text: String, x: Double, y: Double, height: Double = 0.015) -> RecognizedLine {
        RecognizedLine(text: text, x: x, y: y, width: Double(text.count) * 0.0075, height: height)
    }

    private func reading(_ values: [SheetField: Double]) -> SheetReading {
        var reading = SheetReading()
        reading.values = values
        return reading
    }
}
