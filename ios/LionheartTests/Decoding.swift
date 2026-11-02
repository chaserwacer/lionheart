import Foundation
import Testing

@testable import Lionheart

/// Decoding a fixture, so each test reads as its claim rather than as JSON plumbing.
///
/// Nothing here touches the network. Every fixture is a string literal written beside the test that
/// uses it, which is what keeps the premise visible at the point a test fails.
enum Decoding {
    static func decode<T: Decodable>(_ type: T.Type = T.self, _ json: String) throws -> T {
        try Wire.decoder().decode(T.self, from: Data(json.utf8))
    }

    /// A top-level array, read the way an endpoint type reads one.
    static func list<T: Decodable>(_ type: T.Type = T.self, _ json: String) throws -> [T] {
        try Wire.decoder().decode(LenientList<T>.self, from: Data(json.utf8)).elements
    }
}

/// Fixtures named for the domain state they represent.
///
/// `Session.completed(...)` reads as a premise; a literal with eleven members in it does not. Each
/// takes only what the test varies and fills the rest with something valid.
enum Session {
    static let id = "11111111-1111-1111-1111-111111111111"
    static let movementId = "22222222-2222-2222-2222-222222222222"

    static func completed(
        id: String = Self.id,
        date: String = "2026-03-04",
        sets: String = "[]",
        period: String = #"{"$type":"instant","at":"2026-03-04T18:00:00+00:00"}"#,
        effort: String = #"{"$type":"none"}"#,
        decoration: String = #"{"$type":"none"}"#,
        notes: String = ""
    ) -> String {
        """
        {"$type":"completed","trainingSessionId":"\(id)","notes":"\(notes)","provenance":0,
         "trainingProgramId":null,"createdAt":"2026-03-04T18:05:00","date":"\(date)",
         "sets":\(sets),"period":\(period),"effort":\(effort),"decoration":\(decoration)}
        """
    }

    static func planned(
        id: String = Self.id,
        date: String = "2026-03-06",
        plannedFor: String? = nil,
        prescriptions: String = "[]",
        notes: String = ""
    ) -> String {
        """
        {"$type":"planned","trainingSessionId":"\(id)","notes":"\(notes)","provenance":0,
         "trainingProgramId":null,"createdAt":"2026-03-01T09:00:00","date":"\(date)",
         "plannedFor":"\(plannedFor ?? date)","prescriptions":\(prescriptions)}
        """
    }

    static func active(
        id: String = Self.id,
        date: String = "2026-03-04",
        startedAt: String = "2026-03-04T17:30:00+00:00",
        sets: String = "[]"
    ) -> String {
        """
        {"$type":"active","trainingSessionId":"\(id)","notes":"","provenance":0,
         "trainingProgramId":null,"createdAt":"2026-03-04T17:30:00","date":"\(date)",
         "startedAt":"\(startedAt)","sets":\(sets)}
        """
    }

    static func skipped(id: String = Self.id, date: String = "2026-03-02") -> String {
        """
        {"$type":"skipped","trainingSessionId":"\(id)","notes":"","provenance":0,
         "trainingProgramId":null,"createdAt":"2026-03-01T09:00:00","date":"\(date)",
         "plannedFor":"\(date)"}
        """
    }

    /// A lifecycle state no client knows — what a server that gained a case would send.
    static func ofUnknownKind(id: String = Self.id, date: String = "2026-03-04") -> String {
        """
        {"$type":"abandoned","trainingSessionId":"\(id)","notes":"left early","provenance":0,
         "trainingProgramId":null,"createdAt":"2026-03-04T18:05:00","date":"\(date)",
         "abandonedAt":"2026-03-04T18:30:00+00:00"}
        """
    }

    static func liftSet(
        movementId: String = Self.movementId,
        reps: Int = 5,
        weight: Double = 235,
        unit: Int = 1,
        rpe: String = "8.5"
    ) -> String {
        """
        {"$type":"lift","source":{"$type":"formed"},"facets":[],
         "movementDataId":"\(movementId)","reps":\(reps),"weight":\(weight),
         "unit":\(unit),"rpe":\(rpe)}
        """
    }
}

/// Oura fixtures, named for whether the day actually carries a reading.
enum Oura {
    static func day(date: String, readinessScore: Int, sleepScore: Int = 80) -> String {
        """
        {"objectID":"33333333-3333-3333-3333-333333333333","date":"\(date)",
         "readinessData":{"readinessScore":\(readinessScore),"temperatureDeviation":0.1,
           "activityBalance":80,"bodyTemperature":80,"hrvBalance":72,"previousDayActivity":80,
           "previousNight":80,"recoveryIndex":90,"restingHeartRate":48,"sleepBalance":77},
         "sleepData":{"sleepScore":\(sleepScore),"deepSleep":80,"efficiency":94,"latency":80,
           "remSleep":70,"restfulness":75,"timing":80,"totalSleep":27000},
         "activityData":{"activityScore":85,"steps":9000,"activeCalories":500,
           "totalCalories":2600,"targetCalories":500,"meetDailyTargets":80,"moveEveryHour":90,
           "recoveryTime":90,"stayActive":85,"trainingFrequency":80,"trainingVolume":80},
         "resilienceData":{"sleepRecovery":1.2,"daytimeRecovery":1.1,"stress":0.8,
           "resilienceLevel":"solid"}}
        """
    }

    /// A day the ring never synced, as the server stores it: the panels are simply absent.
    static func dayWithoutPanels(date: String) -> String {
        """
        {"objectID":"44444444-4444-4444-4444-444444444444","date":"\(date)",
         "readinessData":null,"sleepData":null,"activityData":null,"resilienceData":null}
        """
    }
}
