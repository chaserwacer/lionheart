import Foundation
import Synchronization

/// The last answer to every read a screen depends on, kept on the device for the signed-in
/// account.
///
/// What is kept is the response **as the server sent it**, not the decoded value. Decoding is
/// lenient and total, so the same bytes decode the same way on the next launch; and keeping bytes
/// means no wire type has to grow an encoder it would otherwise never need, which is a second
/// shape of every type that could drift from the first.
///
/// **Per account, and gone at sign-out.** Answers live under a directory named for the account they
/// were fetched for: an id minted on this device the first time anything is kept after a sign-in,
/// and deleted with everything under it at sign-out. A request is tied to the account current when
/// it was sent, so one that lands after a sign-out keeps nothing, and the next athlete on this
/// phone can never be shown the last one's readiness.
///
/// A class with a `Mutex` because the account is mutable and read from whichever task a request
/// runs on; the file system work happens under the lock so that a sign-out cannot interleave with
/// a write and leave half an account behind.
final class ReadMemory: Sendable {
    /// One kept answer, and when the server gave it.
    struct Recollection: Codable, Sendable {
        let asOf: Date
        let body: Data
    }

    private let root: URL
    private let account: Mutex<UUID?> = Mutex(nil)

    init(root: URL? = nil) {
        self.root = root ?? DeviceFile.applicationDirectory.appending(path: "remembered", directoryHint: .isDirectory)
    }

    private var accountFile: DeviceFile { DeviceFile(directoryURL: root, fileName: "account") }

    /// The account answers are being kept for now, minted if there is none since the last sign-out.
    func currentAccount() -> UUID {
        account.withLock { held in
            if let held { return held }
            let found = accountFile.read()
                .flatMap { String(data: $0, encoding: .utf8) }
                .flatMap(UUID.init(uuidString:))
            let current = found ?? UUID()
            if found == nil { try? accountFile.write(Data(current.uuidString.utf8)) }
            held = current
            return current
        }
    }

    /// Keeps `body` as the answer to `key`, unless the account it was fetched for has since gone.
    func remember(_ body: Data, as key: ReadKey, for fetchedFor: UUID, at asOf: Date) {
        account.withLock { held in
            guard held == fetchedFor,
                  let data = try? JSONEncoder().encode(Recollection(asOf: asOf, body: body))
            else { return }
            try? file(for: key, in: fetchedFor).write(data)
        }
    }

    /// The last answer kept for `key` under `fetchedFor`, or `nil` when there is none.
    func recall(_ key: ReadKey, for fetchedFor: UUID) -> Recollection? {
        account.withLock { held in
            guard held == fetchedFor,
                  let data = file(for: key, in: fetchedFor).read()
            else { return nil }
            return try? JSONDecoder().decode(Recollection.self, from: data)
        }
    }

    /// Deletes every kept answer, for every account. What signing out does.
    func forget() {
        account.withLock { held in
            held = nil
            try? FileManager.default.removeItem(at: root)
        }
    }

    private func file(for key: ReadKey, in account: UUID) -> DeviceFile {
        DeviceFile(
            directoryURL: root.appending(path: account.uuidString, directoryHint: .isDirectory),
            fileName: "\(key.fileName).json")
    }
}

/// Which remembered read an answer belongs to.
///
/// Named for what is read, and where two screens read the same window differently, for the screen
/// too: Today's window of sessions and History's are different answers, and either one standing in
/// for the other would show the wrong days.
struct ReadKey: Hashable, Sendable {
    let name: String

    init(_ name: String) {
        self.name = name
    }

    /// The name as a file name: anything but letters, digits, dots and dashes becomes a dash, so a
    /// key built from a record's identity cannot reach outside its directory.
    var fileName: String {
        String(name.unicodeScalars.map { scalar in
            CharacterSet.alphanumerics.contains(scalar) || scalar == "." || scalar == "-"
                ? Character(scalar) : "-"
        })
    }
}

extension ReadKey {
    static let profileBoot = ReadKey("profile-boot")
    static let movements = ReadKey("movements")
    static let recordSummaries = ReadKey("records.summaries")

    /// One of Today's reads for the day it shows. The current day keeps one answer whatever its
    /// date, so a launch with no signal opens on the last one, marked with its time; a past day
    /// keeps its own, so stepping back through days offline shows each as it was read.
    static func today(_ part: String, showing day: CalendarDay, today: CalendarDay) -> ReadKey {
        ReadKey(day == today ? "today.\(part)" : "today.\(day.wire).\(part)")
    }

    static let trainSessions = ReadKey("train.sessions")
    static let trainRoutines = ReadKey("train.routines")
    static let previousPerformances = ReadKey("train.previous-performances")
    static let bodyweight = ReadKey("bodyweight")

    static let historySessions = ReadKey("history.sessions")

    static let bodyOuraDays = ReadKey("body.oura-days")
    static let bodySleepPeriods = ReadKey("body.sleep-periods")
    static let bodyOuraWorkouts = ReadKey("body.oura-workouts")
    static let bodyOuraTags = ReadKey("body.oura-tags")
    static let bodyWellness = ReadKey("body.wellness")
    static let bodyInjuries = ReadKey("body.injuries")

    static let focusPins = ReadKey("focus.pins")

    /// The thread's newest page, so what Lionheart last said can be read back with no signal.
    static let chatThread = ReadKey("chat.thread")
    static let chatMemories = ReadKey("chat.memories")

    /// The seven days ending today, each kept as one answer whatever its date, so a launch with no
    /// signal opens on the last week read, marked with its time.
    static let trainingVolume = ReadKey("balance.volume")
    static let trainingScore = ReadKey("balance.score")
    static let trainingScoreWeeks = ReadKey("balance.score-weeks")
    static let priorities = ReadKey("balance.priorities")

    /// The records one movement established, for its history.
    static func establishedRecords(movement movementDataId: UUID) -> ReadKey {
        ReadKey("records.established.movement.\(movementDataId.uuidString)")
    }

    /// The records one session established, for its detail.
    static func establishedRecords(session sessionId: UUID) -> ReadKey {
        ReadKey("records.established.session.\(sessionId.uuidString)")
    }

    /// The cardio report for one span, ending today. Kept per span, so switching span offline shows
    /// the one last read for it.
    static func cardio(days: Int) -> ReadKey { ReadKey("body.cardio.\(days)") }
    static func habits(days: Int) -> ReadKey { ReadKey("body.habits.\(days)") }
    static func habitsDay(_ day: CalendarDay) -> ReadKey { ReadKey("habits.day.\(day.wire)") }
    static func heartRate(from: Date, to: Date) -> ReadKey {
        ReadKey("heart-rate.\(Int(from.timeIntervalSince1970)).\(Int(to.timeIntervalSince1970))")
    }

    /// One record chain, by the identity the route is asked with.
    static func recordHistory(_ key: RecordKey) -> ReadKey {
        let subject = key.subject.movementDataId?.uuidString ?? "trail-running"
        let bracket = key.kind.bracket.map { ".\($0)" } ?? ""
        return ReadKey("records.history.\(subject).\(key.kind.wireName)\(bracket)")
    }
}
