import Foundation

/// Distance-and-time work that belongs to no movement: an outdoor run, or one of the endurance
/// activities. The other cardio shape, distance and time on a movement (a rower, a treadmill), is
/// picked as its movement.
///
/// Its own type because the Add sheet offers these beside the catalogue, and each becomes a
/// different set shape: an outdoor run is the domain's trail run, which is the one that holds
/// records; the rest are endurance sets of their activity.
enum CardioWork: Hashable, Sendable, Identifiable {
    case outdoorRun
    case activity(EnduranceActivity)

    /// What the Add sheet offers, commonest first.
    static let offered: [CardioWork] = [
        .outdoorRun, .activity(.indoorRun), .activity(.ride), .activity(.indoorRide), .activity(.hike),
        .activity(.walk),
    ]

    var id: String {
        switch self {
        case .outdoorRun: "outdoorRun"
        case .activity(let activity): activity.wire
        }
    }

    var title: String {
        switch self {
        case .outdoorRun: "Run"
        case .activity(let activity): activity.title
        }
    }

    func matches(_ query: String) -> Bool {
        let words = query.split(whereSeparator: \.isWhitespace)
        return words.allSatisfy { title.range(of: $0, options: [.caseInsensitive, .diacriticInsensitive]) != nil }
    }

    /// The set the entry row opens on: the last of the same work in this workout, so a second
    /// interval reads as the first did, or an empty one in the unit the last cardio was logged in.
    func nextSet(in workout: ActiveWorkout) -> SetCommand {
        if let last = workout.sets.last(where: { isSame(as: $0.command) }) {
            return last.command
        }
        let unit = workout.sets.reversed().lazy.compactMap { Self.distanceUnit(of: $0.command) }.first
            ?? SetDefaults.distanceUnit

        switch self {
        case .outdoorRun:
            return .trailRun(SetCommand.TrailRun(
                distance: 0, unit: unit, duration: 0, elevationGainMeters: 0, rpe: .unrated))
        case .activity(let activity):
            return .endurance(SetCommand.Endurance(
                activity: activity, distance: 0, unit: unit, duration: 0, elevationGainMeters: 0, rpe: .unrated))
        }
    }

    /// The work a set is, for a set with no movement of its own.
    init?(_ command: SetCommand) {
        switch command {
        case .trailRun: self = .outdoorRun
        case .endurance(let set): self = .activity(set.activity)
        case .lift, .distanceTime, .freeform: return nil
        }
    }

    private func isSame(as command: SetCommand) -> Bool {
        CardioWork(command) == self
    }

    private static func distanceUnit(of command: SetCommand) -> DistanceUnit? {
        switch command {
        case .distanceTime(let set): set.unit
        case .trailRun(let set): set.unit
        case .endurance(let set): set.unit
        case .lift, .freeform: nil
        }
    }
}
