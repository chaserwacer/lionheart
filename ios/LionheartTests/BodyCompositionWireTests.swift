import Foundation
import Testing

@testable import Lionheart

/// The scan routes' bytes, written against the server's `BodyCompositionWireTests`.
@Suite("Body-composition wire")
struct BodyCompositionWireTests {
    @Test("A scan decodes with each panel the server named")
    func aScanDecodes() throws {
        let scan = try Decoding.decode(BodyCompositionScan.self, """
            {"id":"0190f3a2-5c1e-7000-8000-00000000a001","day":"2026-09-14","time":"07:41:00",
             "sheet":{"device":"InBody570","unit":1,"height":{"value":71.0,"unit":0},
               "weight":183.4,"skeletalMuscleMass":88.2,"bodyFatMass":30.2,"leanBodyMass":153.2,
               "percentBodyFat":16.5,"bodyMassIndex":25.6,
               "sections":[
                 {"$type":"segmentalLean","segments":{
                   "rightArm":{"mass":9.10,"percentOfReference":118.0},"leftArm":{"mass":9.02,"percentOfReference":116.9},
                   "trunk":{"mass":68.4,"percentOfReference":112.2},"rightLeg":{"mass":22.90,"percentOfReference":106.5},
                   "leftLeg":{"mass":22.71,"percentOfReference":105.6}}},
                 {"$type":"extracellularWaterRatio","value":0.375},
                 {"$type":"impedance","rows":[{"kilohertz":5,"ohms":{"rightArm":290.1,"leftArm":292.4,"trunk":21.9,"rightLeg":262.0,"leftLeg":263.3}}]}
               ]}}
            """)

        #expect(scan.time == ClockTime(hour: 7, minute: 41))
        #expect(scan.sheet.height == Height(value: 71, unit: .inches))
        #expect(scan.sheet.segmentalLean?.leftLeg == SegmentReading(mass: 22.71, percentOfReference: 105.6))
        #expect(scan.sheet.extracellularWaterRatio == 0.375)
        #expect(scan.sheet.impedance?.first?.ohms.trunk == 21.9)
        #expect(scan.sheet.segmentalFat == nil)
        #expect(scan.sheet.isWhollyUnderstood)
    }

    @Test("A panel this build does not know keeps the scan readable but not correctable")
    func anUnknownPanel() throws {
        let scan = try Decoding.decode(BodyCompositionScan.self, """
            {"id":"0190f3a2-5c1e-7000-8000-00000000a001","day":"2026-09-14","time":"07:41:00",
             "sheet":{"device":"InBody770","unit":1,"height":{"value":71.0,"unit":0},
               "weight":183.4,"skeletalMuscleMass":88.2,"bodyFatMass":30.2,"leanBodyMass":153.2,
               "percentBodyFat":16.5,"bodyMassIndex":25.6,
               "sections":[{"$type":"phaseAngle","degrees":7.1},{"$type":"fitnessScore","points":84}]}}
            """)

        #expect(scan.sheet.sections == [.unrecognised("phaseAngle"), .fitnessScore(84)])
        #expect(!scan.sheet.isWhollyUnderstood)
    }

    @Test("A scan goes out in the shape the server reads, with its clock and the bodyweight answer")
    func aSubmissionEncodes() throws {
        let scan = AScan.inBody570()
        let submission = ScanSubmission(day: scan.day, time: scan.time, sheet: scan.sheet, recordsBodyweight: false)

        let json = try #require(try JSONSerialization.jsonObject(with: Wire.encoder().encode(submission)) as? [String: Any])
        let sheet = try #require(json["sheet"] as? [String: Any])
        let sections = try #require(sheet["sections"] as? [[String: Any]])

        #expect(json["day"] as? String == "2026-09-14")
        #expect(json["time"] as? String == "07:41:00")
        #expect(json["recordsBodyweight"] as? Bool == false)
        #expect(sheet["unit"] as? Int == 1)
        #expect(sections.compactMap { $0["$type"] as? String } == [
            "bodyWater", "dryLeanMass", "segmentalLean", "segmentalFat", "extracellularWaterRatio",
            "fitnessScore", "massControl", "basalMetabolicRate", "visceralFatLevel", "impedance",
        ])
    }

    @Test("What goes out decodes back to the same sheet")
    func aSheetRoundTrips() throws {
        let sheet = AScan.inBody570().sheet

        let decoded = try Wire.decoder().decode(BodyCompositionSheet.self, from: Wire.encoder().encode(sheet))

        #expect(decoded == sheet)
    }
}
