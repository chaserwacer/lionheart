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
            heartRate: [.init(at: start, bpm: 131)],
            metrics: [.init(metric: .groundContactTime, samples: [MetricSample(at: start, value: 244)])])

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
        let series = try #require((json["metrics"] as? [[String: Any]])?.first)
        #expect(series["metric"] as? String == "groundContactTime")
        #expect((series["samples"] as? [[String: Any]])?.first?["value"] as? Double == 244)
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
        let deleted = try Decoding.decode(HealthImportOutcome.self, """
            {"$type":"notImported","reason":"deleted","trainingSessionId":null}
            """)
        let unknown = try Decoding.decode(HealthImportOutcome.self, #"{"$type":"queued"}"#)

        let id = try #require(UUID(uuidString: Session.id))
        #expect(imported == .imported(trainingSessionId: id))
        #expect(already == .alreadyImported(trainingSessionId: id))
        #expect(left == .notImported(.readDirectly))
        #expect(deleted == .notImported(.deleted))
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

    @Test("A session from the Strava archive says so, with Strava's activity id")
    func anArchivedSessionNamesItsActivity() throws {
        let json = Session.completed().replacingOccurrences(
            of: #""provenance":0,"#,
            with: #""provenance":0,"source":{"$type":"stravaArchive","activityId":9876543210},"#)

        let source = try Decoding.decode(TrainingSession.self, json).envelope.source

        #expect(source == .stravaArchive(activityId: 9_876_543_210))
        #expect(source.isRecorded)
        #expect(source.recordedBy == "Strava")
        #expect(source.importedFrom == "your Strava archive")
    }

    @Test("An archived run opens onto its recording, and an archived gym session, which has none, does not")
    func onlyAMeasuredArchivedSessionOpensARecording() throws {
        let source = #""provenance":0,"source":{"$type":"stravaArchive","activityId":77},"#
        let run = Session.completed(sets: #"""
            [{"$type":"trailRun","source":{"$type":"formed"},"facets":[],"distance":8.15,"unit":3,
              "duration":"00:43:49","elevationGainMeters":207,"rpe":null}]
            """#).replacingOccurrences(of: #""provenance":0,"#, with: source)
        let gym = Session.completed(sets: #"""
            [{"$type":"freeform","source":{"$type":"texted","rawText":"Weight training, 1:10:00, recorded in Strava"},"facets":[]}]
            """#).replacingOccurrences(of: #""provenance":0,"#, with: source)

        #expect(try Decoding.decode(TrainingSession.self, run).opensRecording)
        #expect(try !Decoding.decode(TrainingSession.self, gym).opensRecording)
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
        #expect(session.source == .loggedSession(trainingSessionId: id, isTrailRun: true, isRecorded: true, activity: nil))
        #expect(session.heartRate == .recorded(averageBpm: 152, peakBpm: 171, coverage: 1, zones: nil, source: .recordingApp))
    }

    @Test("An imported ride in the cardio report names itself a ride")
    func cardioNamesARide() throws {
        let session = try Decoding.decode(CardioSession.self, """
            {"source":{"$type":"loggedSession","trainingSessionId":"\(Session.id)","isTrailRun":false,"isRecorded":true,"activity":"ride"},
             "start":"2026-09-20T07:00:00+02:00","end":"2026-09-20T08:30:00+02:00","distanceMeters":42000,
             "heartRate":{"$type":"notRecorded"}}
            """)

        #expect(session.activityName == "Ride")
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
        let run = try Decoding.decode(RecordedWorkout.self, Self.runJSON)

        #expect(run.recordedBy == "Strava")
        #expect(run.distanceMeters == 2500)
        #expect(run.duration == 1780)
        #expect(run.movingTime == 1750.5)
        #expect(run.pace == 712)
        #expect(run.kilometres.map(\.number) == [1, 2, 3])
        #expect(run.kilometres.last?.distanceMeters == 500)
        #expect(run.miles.count == 2)
        #expect(run.kilometres.first?.averageBpm == 148)
        #expect(run.trace.source == .recordingApp)
        #expect(run.trace.samples.map(\.bpm) == [140, 150])
        #expect(run.route == [RouteCoordinate(latitude: 46.5, longitude: 8.25), RouteCoordinate(latitude: 46.51, longitude: 8.25)])
    }

    @Test("An e-bike ride from the Strava archive reads as recorded in Strava, as a speed, competing on nothing")
    func anArchivedRideNamesStrava() throws {
        let json = Self.runJSON.replacingOccurrences(
            of: Self.healthSource,
            with: #""source":{"$type":"stravaArchive","activityId":1234},"activity":"eBikeRide","#)

        let ride = try Decoding.decode(RecordedWorkout.self, json)

        #expect(ride.source == .stravaArchive(activityId: 1234))
        #expect(ride.recordedBy == "Strava")
        #expect(ride.activity == .endurance(.eBikeRide))
        #expect(ride.activity.readsAsSpeed)
        #expect(EnduranceActivity.eBikeRide.recordSubject == nil)
    }

    @Test("A run with no activity named is a run, and one with no metrics has none")
    func aRunFromAnOlderServerIsARun() throws {
        let run = try Decoding.decode(RecordedWorkout.self, Self.runJSON)

        #expect(run.activity == .run)
        #expect(run.metrics.isEmpty)
    }

    @Test("A ride decodes its activity and each metric, and a metric this build does not know is kept by name")
    func aRideDecodesItsMetrics() throws {
        let json = Self.runJSON.replacingOccurrences(
            of: #""route":["#,
            with: #""activity":"ride","metrics":[{"metric":"power","average":212,"peak":640,"samples":[{"at":"2026-09-20T05:00:00+00:00","value":200},{"at":"2026-09-20T05:00:01+00:00","value":224}]},{"metric":"torque","average":3,"peak":4,"samples":[]},{"metric":"cadence"}],"route":["#)

        let ride = try Decoding.decode(RecordedWorkout.self, json)

        #expect(ride.activity == .endurance(.ride))
        #expect(ride.activity.readsAsSpeed)
        #expect(ride.metrics.map(\.metric) == [.power, .unrecognised("torque")])
        #expect(ride.metric(.power)?.samples.map(\.value) == [200, 224])
    }

    @Test("A split that cannot be read is dropped and the run still loads")
    func aMalformedSplitDegrades() throws {
        let json = Self.runJSON.replacingOccurrences(of: #""number":2,"#, with: #""number":"two","#)

        #expect(try Decoding.decode(RecordedWorkout.self, json).kilometres.map(\.number) == [1, 3])
    }

    static let healthSource =
        #""source":{"$type":"appleHealth","workoutId":"8D0C3A52-6C1E-4D7B-9A43-2F7D1E6B5C90","recordedBy":{"name":"Strava","bundleId":"com.strava.stravaride"}},"#

    static let runJSON = """
        {"trainingSessionId":"\(Session.id)",\(healthSource)
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
