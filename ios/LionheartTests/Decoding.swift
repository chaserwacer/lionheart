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

    /// A day with sleep and nothing else — the shape that proves the panels arrive independently.
    static func dayWithSleepOnly(date: String, sleepScore: Int = 74) -> String {
        """
        {"objectID":"44444444-4444-4444-4444-444444444445","date":"\(date)",
         "readinessData":null,
         "sleepData":{"sleepScore":\(sleepScore),"deepSleep":4800,"efficiency":91,
           "remSleep":5400,"totalSleep":26100},
         "activityData":null,"resilienceData":null}
        """
    }
}

/// Self-reported wellness, as `GET /api/wellness/get-range` sends it.
///
/// `overallScore` is written into the fixture rather than computed, because the claim under test is
/// that the client prints the server's number instead of deriving a second one.
enum Wellness {
    static func reading(
        date: String,
        motivation: Int = 4,
        stress: Int = 2,
        mood: Int = 4,
        energy: Int = 4,
        overall: Double = 3.75
    ) -> String {
        """
        {"stateID":"55555555-5555-5555-5555-555555555555",
         "userID":"66666666-6666-6666-6666-666666666666","date":"\(date)",
         "motivationScore":\(motivation),"stressScore":\(stress),"moodScore":\(mood),
         "energyScore":\(energy),"overallScore":\(overall)}
        """
    }
}

/// Injuries and the events logged against them.
enum Tracked {
    static let injuryId = "77777777-7777-7777-7777-777777777777"

    static func injury(
        id: String = Self.injuryId,
        name: String = "Right shoulder impingement",
        injuryDate: String = "2026-01-20",
        isActive: Bool = true,
        events: String = "[]"
    ) -> String {
        """
        {"injuryID":"\(id)","name":"\(name)","notes":"Aggravated by overhead pressing.",
         "injuryDate":"\(injuryDate)","isActive":\(isActive),"injuryEvents":\(events)}
        """
    }

    /// `injuryType` is 0 for a treatment and 1 for a check-in — integer-valued, like every enum on
    /// this wire.
    static func event(
        id: String,
        painLevel: Int,
        kind: Int = 1,
        createdAt: String = "2026-02-01T09:00:00",
        notes: String = ""
    ) -> String {
        """
        {"injuryEventID":"\(id)","trainingSessionID":null,"movementIDs":[],"notes":"\(notes)",
         "painLevel":\(painLevel),"injuryType":\(kind),"creationTime":"\(createdAt)"}
        """
    }
}

/// The movement catalogue, which is the only thing that can put a name on a set.
enum Catalogue {
    static func movement(
        id: String,
        base: String,
        equipment: String = "Barbell",
        modifier: String? = nil
    ) -> String {
        let modifierId = "88888888-8888-8888-8888-888888888888"
        let modifierJson = modifier.map {
            #"{"movementModifierID":"\#(modifierId)","name":"\#($0)"}"#
        } ?? "null"
        return """
        {"movementDataID":"\(id)","equipmentID":"99999999-9999-9999-9999-999999999999",
         "equipment":{"equipmentID":"99999999-9999-9999-9999-999999999999",
           "name":"\(equipment)","enabled":true},
         "movementBaseID":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
         "movementBase":{"movementBaseID":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
           "name":"\(base)","description":"","muscleGroups":[]},
         "movementModifierID":\(modifier == nil ? "null" : "\"\(modifierId)\""),
         "movementModifier":\(modifierJson)}
        """
    }
}

/// Chat conversations and their turns.
///
/// `role` is the discriminator the transcript depends on: 0 the athlete, 1 the model, 2 a stored
/// tool result, 3 the system prompt. Without it a client has to guess the speaker from the ordering
/// of a projection that merges two tables.
enum Chat {
    static let conversationId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"

    static func conversation(
        id: String = Self.conversationId,
        name: String = "Should I press overhead?",
        lastUpdate: String = "2026-03-04T18:05:00",
        messages: String = "[]"
    ) -> String {
        """
        {"chatConversationID":"\(id)","createdAt":"2026-03-04T17:00:00",
         "lastUpdate":"\(lastUpdate)","name":"\(name)","messages":\(messages)}
        """
    }

    /// What the list route actually sends: no turns at all, spelled `null`.
    static func conversationWithNullMessages(id: String = Self.conversationId) -> String {
        """
        {"chatConversationID":"\(id)","createdAt":"2026-03-04T17:00:00",
         "lastUpdate":"2026-03-04T18:05:00","name":"Listed","messages":null}
        """
    }

    static func message(
        id: String,
        role: Int,
        content: String,
        createdAt: String = "2026-03-04T17:30:00"
    ) -> String {
        """
        {"chatMessageItemID":"\(id)","chatConversationID":"\(Self.conversationId)",
         "creationTime":"\(createdAt)","tokenCount":19,"content":"\(content)","role":\(role)}
        """
    }
}

/// Personal records, as the summaries route groups them.
enum Records {
    static func summary(
        movementId: String,
        base: String,
        strength: String = "null",
        volume: String = "null"
    ) -> String {
        """
        {"movementDataID":"\(movementId)",
         "movementData":\(Catalogue.movement(id: movementId, base: base)),
         "strengthPR":\(strength),"volumePR":\(volume),"lastPRDate":"2026-03-01T18:00:00"}
        """
    }

    /// `prType` is 0 for strength and 1 for volume. `volume` is the server's own product of weight
    /// and reps, written here as a value the client must print rather than recompute.
    static func record(
        id: String,
        movementId: String,
        kind: Int,
        weight: Double,
        reps: Int,
        volume: Double,
        previousAt: String = "null"
    ) -> String {
        """
        {"personalRecordID":"\(id)","movementDataID":"\(movementId)",
         "movementData":\(Catalogue.movement(id: movementId, base: "Bench Press")),
         "prType":\(kind),"weight":\(weight),"weightUnit":0,"reps":\(reps),"volume":\(volume),
         "createdAt":"2026-03-04T18:00:00","previousPRCreatedAt":\(previousAt),
         "previousPersonalRecordID":null,"isActive":true}
        """
    }
}

/// The athlete context card, as the profile route sends it.
enum ContextCard {
    static func card(
        stable: String = "Trains four times a week around a shoulder.",
        recent: String = "Wellness: latest 2026-03-03 overall 4.0."
    ) -> String {
        """
        {"stableProfileText":"\(stable)","stableProfileAsOf":"2026-03-01T06:00:00",
         "recentStateText":"\(recent)","recentStateAsOf":"2026-03-04T06:00:00"}
        """
    }
}
