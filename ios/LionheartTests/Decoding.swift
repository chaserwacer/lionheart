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

/// Oura fixtures, named for which collections the day actually carries.
///
/// Every collection is independently absent on the wire, and a day the ring recorded nothing for
/// is not sent at all — so these fixtures vary presence rather than inventing zeroed panels.
enum Oura {
    static func day(
        date: String,
        readinessScore: String = "76",
        sleepScore: String = "80",
        resilience: String = "null"
    ) -> String {
        """
        {"day":"\(date)",
         "activity":{"score":85,"contributors":{"meetDailyTargets":100,"moveEveryHour":90,
            "recoveryTime":97,"stayActive":85,"trainingFrequency":100,"trainingVolume":100},
           "steps":9000,"activeCalories":500,"totalCalories":2600,"targetCalories":500,
           "equivalentWalkingDistanceMeters":14556,"metersToTarget":-3600,"targetMeters":12000,
           "inactivityAlerts":0,"averageMet":1.6875,"lowActivityMetMinutes":171,
           "mediumActivityMetMinutes":420,"highActivityMetMinutes":36,"sedentaryMetMinutes":14,
           "restingTimeSeconds":26580,"sedentaryTimeSeconds":32760,"lowActivityTimeSeconds":15300,
           "mediumActivityTimeSeconds":7140,"highActivityTimeSeconds":240,
           "nonWearTimeSeconds":4380},
         "readiness":{"score":\(readinessScore),"temperatureDeviation":-0.05,
           "temperatureTrendDeviation":0.11,
           "contributors":{"activityBalance":83,"bodyTemperature":100,"hrvBalance":72,
             "previousDayActivity":83,"previousNight":59,"recoveryIndex":90,
             "restingHeartRate":81,"sleepBalance":77,"sleepRegularity":90}},
         "sleep":{"score":\(sleepScore),
           "contributors":{"deepSleep":80,"efficiency":94,"latency":80,"remSleep":70,
             "restfulness":75,"timing":80,"totalSleep":90}},
         "spo2":{"averageSpo2Percentage":96.5,"breathingDisturbanceIndex":4},
         "stress":{"stressHighSeconds":10800,"recoveryHighSeconds":21600,
           "daySummary":"normal"},
         "resilience":\(resilience)}
        """
    }

    /// A day whose collections are all absent — the shape the server sends when Oura filed
    /// something for the day but nothing this client reads.
    static func dayWithoutPanels(date: String) -> String {
        """
        {"day":"\(date)","activity":null,"readiness":null,"sleep":null,
         "spo2":null,"stress":null,"resilience":null}
        """
    }

    /// A day with sleep and nothing else — the shape that proves the collections arrive
    /// independently of one another.
    static func dayWithSleepOnly(date: String, sleepScore: Int = 74) -> String {
        """
        {"day":"\(date)","activity":null,"readiness":null,
         "sleep":{"score":\(sleepScore),
           "contributors":{"deepSleep":60,"efficiency":91,"latency":70,"remSleep":65,
             "restfulness":70,"timing":75,"totalSleep":80}},
         "spo2":null,"stress":null,"resilience":null}
        """
    }

    /// Resilience, which the athlete's archive has no history for — so it is the one collection a
    /// day usually arrives without.
    static let resilience = """
        {"level":"solid","contributors":{"sleepRecovery":1.2,"daytimeRecovery":1.1,"stress":0.8}}
        """

    /// One sleep period in the event envelope every Oura event shares.
    ///
    /// `day` travels beside `period` because Oura files a night that began before midnight under
    /// the waking day — so a fixture that derived one from the other could not express the case.
    static func sleepPeriod(
        id: String,
        day: String,
        start: String,
        end: String,
        isMainSleep: Bool = true,
        averageHeartRate: String = "52.4",
        samples: String = "[48,null,51]"
    ) -> String {
        """
        {"ouraId":"\(id)","day":"\(day)",
         "period":{"$type":"interval","start":"\(start)","end":"\(end)"},
         "reading":{"period":{"$type":"interval","start":"\(start)","end":"\(end)"},
           "kind":"\(isMainSleep ? "long_sleep" : "sleep")","periodIndex":0,
           "timeInBedSeconds":28800,"totalSleepSeconds":26100,"deepSleepSeconds":4800,
           "lightSleepSeconds":15900,"remSleepSeconds":5400,"awakeSeconds":2700,
           "latencySeconds":600,"efficiency":91,"restlessPeriods":12,
           "averageHeartRate":\(averageHeartRate),"lowestHeartRate":46,"averageHrv":58,
           "averageBreath":14.5,"readinessScoreDelta":null,"sleepScoreDelta":null,
           "algorithmVersion":"v2","analysisReason":"long_sleep_analysis",
           "hypnogram":"4433221",
           "heartRate":{"intervalSeconds":300,"samples":\(samples),"startedAt":"\(start)"},
           "hrv":{"intervalSeconds":300,"samples":\(samples),"startedAt":"\(start)"},
           "isMainSleep":\(isMainSleep)}}
        """
    }

    static func workout(
        id: String, day: String, activity: String = "walking", distance: String = "735.2"
    ) -> String {
        """
        {"ouraId":"\(id)","day":"\(day)",
         "period":{"$type":"interval","start":"\(day)T20:59:00+00:00",
           "end":"\(day)T21:20:00+00:00"},
         "reading":{"period":{"$type":"interval","start":"\(day)T20:59:00+00:00",
             "end":"\(day)T21:20:00+00:00"},
           "activity":"\(activity)","intensity":"moderate","source":"confirmed","label":null,
           "calories":69.47,"distanceMeters":\(distance)}}
        """
    }

    static func tag(id: String, day: String, codes: String = #"["tag_sleep_sauna"]"#) -> String {
        """
        {"ouraId":"\(id)","day":"\(day)",
         "period":{"$type":"instant","at":"\(day)T19:01:56+00:00"},
         "reading":{"period":{"$type":"instant","at":"\(day)T19:01:56+00:00"},
           "codes":\(codes),"text":""}}
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
