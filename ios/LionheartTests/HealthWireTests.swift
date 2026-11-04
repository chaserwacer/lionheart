import Foundation
import Testing

@testable import Lionheart

/// The Apple Health shapes on the wire: the workout the phone offers, the server's answer, a run's
/// page, and where a session came from. Literal fixtures, invented — no value here is anyone's run.
@Suite("Apple Health on the wire")
struct HealthWireTests {
    // MARK: - The offer

    @Test("A workout goes out with its times at the zone it was recorded in and its time as a TimeSpan")
    func anOfferEncodesAsTheServerReadsIt() throws {
        let start = try #require(WireDate.instant(from: "2026-09-20T05:00:00Z"))
        let workout = HealthWorkoutImport(
            workoutId: try #require(UUID(uuidString: "8D0C3A52-6C1E-4D7B-9A43-2F7D1E6B5C90")),
            activity: "running",
            indoor: false,
            appName: "Strava",
            appBundleId: "com.strava.stravaride",
            start: start,
            end: start.addingTimeInterval(1800),
            timeZone: try #require(TimeZone(identifier: "Europe/Zurich")),
            duration: 1780,
            distanceMeters: nil,
            elevationAscendedMeters: 42,
            route: [.init(at: start, latitude: 46.5, longitude: 8.25, altitude: nil)],
            heartRate: [.init(at: start, bpm: 131)])

        let json = try #require(
            JSONSerialization.jsonObject(with: Wire.encoder().encode(workout)) as? [String: Any])

        #expect(json["workoutId"] as? String == "8D0C3A52-6C1E-4D7B-9A43-2F7D1E6B5C90")
        #expect(json["start"] as? String == "2026-09-20T07:00:00.000+02:00")
        #expect(json["duration"] as? String == "00:29:40")
        #expect(json["distanceMeters"] is NSNull)
        #expect(json["elevationAscendedMeters"] as? Double == 42)
        let fix = try #require((json["route"] as? [[String: Any]])?.first)
        #expect(fix["altitude"] is NSNull)
        #expect(fix["latitude"] as? Double == 46.5)
        #expect((json["heartRate"] as? [[String: Any]])?.first?["bpm"] as? Int == 131)
    }

    // MARK: - The answer

    @Test("Each answer to an offer decodes to its case")
    func outcomesDecode() throws {
        let imported = try Decoding.decode(HealthImportOutcome.self, """
            {"$type":"imported","session":\(Session.completed())}
            """)
        let already = try Decoding.decode(HealthImportOutcome.self, """
            {"$type":"alreadyImported","trainingSessionId":"\(Session.id)"}
            """)
        let left = try Decoding.decode(HealthImportOutcome.self, """
            {"$type":"notImported","reason":"readDirectly","trainingSessionId":null}
            """)
        let unknown = try Decoding.decode(HealthImportOutcome.self, #"{"$type":"queued"}"#)

        let id = try #require(UUID(uuidString: Session.id))
        #expect(imported == .imported(trainingSessionId: id))
        #expect(already == .alreadyImported(trainingSessionId: id))
        #expect(left == .notImported(.readDirectly))
        #expect(unknown == .unrecognised("queued"))
    }

    // MARK: - Where a session came from

    @Test("A session imported from Apple Health says so, with the app that recorded it")
    func anImportedSessionNamesItsSource() throws {
        let json = Session.completed().replacingOccurrences(
            of: #""provenance":0,"#,
            with: #""provenance":0,"source":{"$type":"appleHealth","workoutId":"8D0C3A52-6C1E-4D7B-9A43-2F7D1E6B5C90","recordedBy":{"name":"Strava","bundleId":"com.strava.stravaride"}},"#)

        let session = try Decoding.decode(TrainingSession.self, json)

        #expect(session.envelope.source == .appleHealth(
            workoutId: try #require(UUID(uuidString: "8D0C3A52-6C1E-4D7B-9A43-2F7D1E6B5C90")), recordedBy: "Strava"))
        #expect(session.envelope.source.isRecorded)
    }

    @Test("A session with no source read is the athlete's own log")
    func noSourceIsTheAthletesLog() throws {
        #expect(try Decoding.decode(TrainingSession.self, Session.completed()).envelope.source == .athleteLog)
    }

    // MARK: - The cardio report

    @Test("A logged session in the cardio report says whether it was imported, and its heart rate whose it is")
    func cardioSaysWhoseHeartRate() throws {
        let session = try Decoding.decode(CardioSession.self, """
            {"source":{"$type":"loggedSession","trainingSessionId":"\(Session.id)","isTrailRun":true,"isRecorded":true},
             "start":"2026-09-20T07:00:00+02:00","end":"2026-09-20T07:30:00+02:00","distanceMeters":5000,
             "heartRate":{"$type":"recorded","averageBpm":152,"peakBpm":171,"coverage":1,"zones":null,"source":"appleHealth"}}
            """)

        let id = try #require(UUID(uuidString: Session.id))
        #expect(session.source == .loggedSession(trainingSessionId: id, isTrailRun: true, isRecorded: true))
        #expect(session.heartRate == .recorded(averageBpm: 152, peakBpm: 171, coverage: 1, zones: nil, source: .appleHealth))
    }

    @Test("A heart rate read before sources were sent is the ring's")
    func aHeartRateWithNoSourceIsTheRings() throws {
        let heartRate = try Decoding.decode(SessionHeartRate.self, """
            {"$type":"recorded","averageBpm":120,"peakBpm":140,"coverage":0.8,"zones":null}
            """)

        #expect(heartRate == .recorded(averageBpm: 120, peakBpm: 140, coverage: 0.8, zones: nil, source: .ring))
    }

    // MARK: - A run's page

    @Test("A run decodes its figures, its splits in both units, its trace and its route")
    func aRunDecodes() throws {
        let run = try Decoding.decode(RecordedRun.self, Self.runJSON)

        #expect(run.recordedBy == "Strava")
        #expect(run.distanceMeters == 2500)
        #expect(run.duration == 1780)
        #expect(run.movingTime == 1750.5)
        #expect(run.pace == 712)
        #expect(run.kilometres.map(\.number) == [1, 2, 3])
        #expect(run.kilometres.last?.distanceMeters == 500)
        #expect(run.miles.count == 2)
        #expect(run.kilometres.first?.averageBpm == 148)
        #expect(run.trace.source == .appleHealth)
        #expect(run.trace.samples.map(\.bpm) == [140, 150])
        #expect(run.route == [RouteCoordinate(latitude: 46.5, longitude: 8.25), RouteCoordinate(latitude: 46.51, longitude: 8.25)])
    }

    @Test("A split that cannot be read is dropped and the run still loads")
    func aMalformedSplitDegrades() throws {
        let json = Self.runJSON.replacingOccurrences(of: #""number":2,"#, with: #""number":"two","#)

        #expect(try Decoding.decode(RecordedRun.self, json).kilometres.map(\.number) == [1, 3])
    }

    static let runJSON = """
        {"trainingSessionId":"\(Session.id)","recordedBy":{"name":"Strava","bundleId":"com.strava.stravaride"},
         "start":"2026-09-20T07:00:00+02:00","end":"2026-09-20T07:30:00+02:00",
         "distanceMeters":2500,"duration":"00:29:40","movingTime":"00:29:10.5000000","pace":"00:11:52",
         "movingPace":"00:11:40.2000000","climbMeters":24,"descentMeters":21,
         "heartRate":{"$type":"recorded","averageBpm":150,"peakBpm":170,"coverage":1,"zones":null,"source":"appleHealth"},
         "maxHeartRate":{"$type":"agePredicted","bpm":187,"age":30,"observedPeak":null},
         "trace":{"source":"appleHealth","samples":[{"at":"2026-09-20T05:00:00+00:00","bpm":140},{"at":"2026-09-20T05:00:05+00:00","bpm":150}]},
         "kilometres":[
           {"number":1,"distanceMeters":1000,"elapsed":"00:11:52","moving":"00:11:40","pace":"00:11:40","climbMeters":10,"descentMeters":0,"averageBpm":148},
           {"number":2,"distanceMeters":1000,"elapsed":"00:11:52","moving":"00:11:40","pace":"00:11:40","climbMeters":8,"descentMeters":9,"averageBpm":151},
           {"number":3,"distanceMeters":500,"elapsed":"00:05:56","moving":"00:05:50","pace":"00:11:40","climbMeters":6,"descentMeters":12,"averageBpm":null}],
         "miles":[
           {"number":1,"distanceMeters":1609.344,"elapsed":"00:19:06","moving":"00:18:46","pace":"00:18:46","climbMeters":14,"descentMeters":3,"averageBpm":149},
           {"number":2,"distanceMeters":890.656,"elapsed":"00:10:34","moving":"00:10:24","pace":"00:18:46","climbMeters":10,"descentMeters":18,"averageBpm":null}],
         "route":[{"latitude":46.5,"longitude":8.25},{"latitude":46.51,"longitude":8.25}]}
        """
}
