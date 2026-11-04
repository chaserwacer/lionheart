import SwiftUI

// The colours the recovery, cardio and movement charts encode with, defined once so a stage, a
// zone or a class is the same colour on every screen. Each carries information — position in a
// lane already says which stage it is, and the colour repeats it so a strip read at a glance still
// reads — and each is a system colour, so light and dark stay correct.

extension SleepStage {
    /// Apple's own sleep-stage convention (Health): deep the darkest, REM the lightest, awake grey.
    var colour: Color {
        switch self {
        case .deep: .indigo
        case .light: .blue
        case .rem: .cyan
        case .awake: Color(.systemGray2)
        }
    }
}

extension ActivityLane {
    /// Intensity as the accent's strength — Brick, deepening with effort — and stillness in greys,
    /// with sleep in the night's indigo.
    var colour: Color {
        switch self {
        case .high: Tokens.accent
        case .medium: Tokens.accent.opacity(0.7)
        case .low: Tokens.accent.opacity(0.42)
        case .inactive: Color(.systemGray)
        case .rest: Color(.systemGray3)
        case .asleep: .indigo.opacity(0.7)
        }
    }
}

extension HourSegment.Kind {
    var colour: Color {
        switch self {
        case .resting: .indigo.opacity(0.35)
        case .sitting: Color(.systemGray)
        case .low: Tokens.accent.opacity(0.42)
        case .medium: Tokens.accent.opacity(0.7)
        case .high: Tokens.accent
        }
    }
}

enum ZoneColour {
    /// Zone 1 to 5 as one hue deepening with intensity, so the order of the zones is the order of
    /// the colours and no zone reads as a warning. The faintest stays strong enough to tell from
    /// the grouped background in dark mode.
    static func of(_ zone: Int) -> Color {
        Tokens.accent.opacity([0.3, 0.48, 0.66, 0.84, 1.0][max(0, min(4, zone - 1))])
    }
}
