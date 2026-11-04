import Foundation

/// The shape of a movement's work — the server's `MovementPattern`.
enum MovementPattern: Hashable, Sendable {
    case horizontalPush, verticalPush, horizontalPull, verticalPull, squat, hinge, singleLeg
    /// A single-joint accessory: a curl, a raise, an extension.
    case isolation
    /// Core work, carries, sleds.
    case other
    case unrecognised(String)

    /// Every pattern this build knows, in the order a lifter lists them.
    static let known: [MovementPattern] = [
        .horizontalPush, .verticalPush, .horizontalPull, .verticalPull, .squat, .hinge, .singleLeg,
        .isolation, .other,
    ]

    init(wire: String) {
        self = Self.known.first { $0.wire == wire } ?? .unrecognised(wire)
    }

    var wire: String {
        switch self {
        case .horizontalPush: "horizontalPush"
        case .verticalPush: "verticalPush"
        case .horizontalPull: "horizontalPull"
        case .verticalPull: "verticalPull"
        case .squat: "squat"
        case .hinge: "hinge"
        case .singleLeg: "singleLeg"
        case .isolation: "isolation"
        case .other: "other"
        case .unrecognised(let wire): wire
        }
    }

    var name: String {
        switch self {
        case .horizontalPush: "Horizontal push"
        case .verticalPush: "Vertical push"
        case .horizontalPull: "Horizontal pull"
        case .verticalPull: "Vertical pull"
        case .squat: "Squat"
        case .hinge: "Hinge"
        case .singleLeg: "Single-leg"
        case .isolation: "Isolation"
        case .other: "Other"
        case .unrecognised(let wire): wire
        }
    }
}

/// Push, pull or legs — and core, for work in none of them. The server's `PatternFamily`.
enum PatternFamily: Hashable, Sendable {
    case push, pull, legs, core
    case unrecognised(String)

    /// The three a week's lifting is judged on, in the order the athlete says them.
    static let judged: [PatternFamily] = [.push, .pull, .legs]

    init(wire: String) {
        switch wire {
        case "push": self = .push
        case "pull": self = .pull
        case "legs": self = .legs
        case "core": self = .core
        default: self = .unrecognised(wire)
        }
    }

    var wire: String {
        switch self {
        case .push: "push"
        case .pull: "pull"
        case .legs: "legs"
        case .core: "core"
        case .unrecognised(let wire): wire
        }
    }

    var name: String {
        switch self {
        case .push: "Push"
        case .pull: "Pull"
        case .legs: "Legs"
        case .core: "Core"
        case .unrecognised(let wire): wire
        }
    }

    /// "push and pull", "push, pull and legs": families named together mid-sentence.
    static func listed(_ families: [PatternFamily]) -> String {
        let names = families.map { $0.name.lowercased() }
        guard names.count > 1 else { return names.first ?? "" }
        return names.dropLast().joined(separator: ", ") + " and " + names[names.count - 1]
    }
}

/// One of the sixteen muscle groups weekly volume is counted against — the server's `MuscleGroup`.
enum MuscleGroup: Hashable, Sendable {
    case chest, frontDelts, sideDelts, rearDelts, triceps, lats, upperBack, biceps, forearms
    case quads, hamstrings, glutes, adductors, calves, lowerBack, abs
    case unrecognised(String)

    static let known: [MuscleGroup] = [
        .chest, .frontDelts, .sideDelts, .rearDelts, .triceps, .lats, .upperBack, .biceps, .forearms,
        .quads, .hamstrings, .glutes, .adductors, .calves, .lowerBack, .abs,
    ]

    init(wire: String) {
        self = Self.known.first { $0.wire == wire } ?? .unrecognised(wire)
    }

    var wire: String {
        switch self {
        case .chest: "chest"
        case .frontDelts: "frontDelts"
        case .sideDelts: "sideDelts"
        case .rearDelts: "rearDelts"
        case .triceps: "triceps"
        case .lats: "lats"
        case .upperBack: "upperBack"
        case .biceps: "biceps"
        case .forearms: "forearms"
        case .quads: "quads"
        case .hamstrings: "hamstrings"
        case .glutes: "glutes"
        case .adductors: "adductors"
        case .calves: "calves"
        case .lowerBack: "lowerBack"
        case .abs: "abs"
        case .unrecognised(let wire): wire
        }
    }

    var name: String {
        switch self {
        case .chest: "Chest"
        case .frontDelts: "Front delts"
        case .sideDelts: "Side delts"
        case .rearDelts: "Rear delts"
        case .triceps: "Triceps"
        case .lats: "Lats"
        case .upperBack: "Upper back"
        case .biceps: "Biceps"
        case .forearms: "Forearms"
        case .quads: "Quads"
        case .hamstrings: "Hamstrings"
        case .glutes: "Glutes"
        case .adductors: "Adductors"
        case .calves: "Calves"
        case .lowerBack: "Lower back"
        case .abs: "Abs"
        case .unrecognised(let wire): wire
        }
    }

}

/// What a movement base trains: its pattern, and its primary and secondary muscle groups.
struct MovementClassification: Equatable, Sendable {
    let pattern: MovementPattern
    let primary: [MuscleGroup]
    let secondary: [MuscleGroup]

    var isCompound: Bool {
        switch pattern {
        case .horizontalPush, .verticalPush, .horizontalPull, .verticalPull, .squat, .hinge, .singleLeg: true
        case .isolation, .other, .unrecognised: false
        }
    }
}

/// Where a classification came from, and so how far to trust it.
enum ClassificationSource: Equatable, Sendable {
    /// Stated or confirmed by the athlete.
    case athlete
    /// The shipped map's entry for the base.
    case map
    /// The map's entry, marked a judgement call, with why.
    case mapUnsure(reason: String)
    case unrecognised(String)
}

/// What a movement trains as the server reads it: the athlete's statement, else the map's entry,
/// else nothing.
enum ClassificationStanding: Equatable, Sendable {
    /// Neither stated nor in the map: its sets are counted nowhere until it is classified.
    case unclassified
    /// `family` is the one it counts toward, as the server reads it: the family a priority must match.
    case classified(MovementClassification, source: ClassificationSource, family: PatternFamily)
    case unrecognised(String)

    var classification: MovementClassification? {
        guard case .classified(let classification, _, _) = self else { return nil }
        return classification
    }

    var family: PatternFamily? {
        guard case .classified(_, _, let family) = self else { return nil }
        return family
    }
}

extension MovementClassification: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        self.init(
            pattern: MovementPattern(wire: container.lenient("pattern", or: "")),
            primary: container.lenientList(String.self, "primary").map(MuscleGroup.init(wire:)),
            secondary: container.lenientList(String.self, "secondary").map(MuscleGroup.init(wire:)))
    }
}

extension ClassificationSource: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "athlete": self = .athlete
        case "map": self = .map
        case "mapUnsure": self = .mapUnsure(reason: container.lenient("reason", or: ""))
        default: self = .unrecognised(type)
        }
    }
}

extension ClassificationStanding: Decodable {
    init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: WireKey.self)
        let type = container.wireType
        switch type {
        case "unclassified":
            self = .unclassified
        case "classified":
            guard let classification = container.lenient(MovementClassification.self, "classification") else {
                self = .unrecognised(type)
                return
            }
            self = .classified(
                classification,
                source: container.lenient(ClassificationSource.self, "source") ?? .unrecognised(""),
                family: PatternFamily(wire: container.lenient("family", or: "")))
        default:
            self = .unrecognised(type)
        }
    }
}
