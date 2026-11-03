import Foundation

/// A sleep stage, as Oura's hypnogram encodes it.
///
/// The digits are Oura's documented ones (OpenAPI 1.43, `sleep_phase_5_min`): 1 deep, 2 light,
/// 3 REM, 4 awake. They were checked against the athlete's own archive before being trusted: of
/// every way of assigning the four digits to the four stages, Oura's is the one whose slot counts
/// land closest to Oura's own stage durations for the same nights, by a wide margin.
enum SleepStage: CaseIterable, Hashable, Sendable {
    // Top to bottom, the order a hypnogram is drawn in.
    case awake, rem, light, deep

    init?(digit: Character) {
        switch digit {
        case "1": self = .deep
        case "2": self = .light
        case "3": self = .rem
        case "4": self = .awake
        default: return nil
        }
    }

    var name: String {
        switch self {
        case .awake: "Awake"
        case .rem: "REM"
        case .light: "Light"
        case .deep: "Deep"
        }
    }
}

/// A stretch of one stage.
struct StageRun: Equatable, Sendable, Identifiable {
    let stage: SleepStage
    let start: Date
    let end: Date

    var id: Date { start }
}

enum Hypnogram {
    /// The length of one hypnogram slot.
    static let slotSeconds: TimeInterval = 300

    /// The night's hypnogram as consecutive runs of one stage, from the period's start.
    ///
    /// A digit outside Oura's documented four leaves a gap rather than a guessed stage.
    static func runs(of night: OuraSleepPeriod, from start: Date) -> [StageRun] {
        let stages = night.hypnogram.map(SleepStage.init(digit:))
        var runs: [StageRun] = []
        var index = 0
        while index < stages.count {
            var next = index + 1
            while next < stages.count, stages[next] == stages[index] { next += 1 }
            if let stage = stages[index] {
                runs.append(StageRun(
                    stage: stage,
                    start: start.addingTimeInterval(Double(index) * slotSeconds),
                    end: start.addingTimeInterval(Double(next) * slotSeconds)))
            }
            index = next
        }
        return runs
    }
}

/// The night's stage totals as shares of the time asleep — what the stage bar draws.
struct StageShare: Equatable, Sendable, Identifiable {
    let stage: SleepStage
    let seconds: Int
    /// Of the time in bed, so awake time is part of the whole.
    let fraction: Double

    var id: SleepStage { stage }

    /// Oura's own stage durations, which are the figures of record; the hypnogram's slots are their
    /// shape, not a second total.
    static func of(_ night: OuraSleepPeriod) -> [StageShare] {
        let parts: [(SleepStage, Int)] = [
            (.awake, night.awakeSeconds), (.rem, night.remSleepSeconds),
            (.light, night.lightSleepSeconds), (.deep, night.deepSleepSeconds),
        ]
        let total = Double(parts.reduce(0) { $0 + $1.1 })
        guard total > 0 else { return [] }
        return parts.map { StageShare(stage: $0.0, seconds: $0.1, fraction: Double($0.1) / total) }
    }
}
