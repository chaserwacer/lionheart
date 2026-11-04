import SwiftUI

/// The mark in motion, standing in for a spinner where the whole screen waits — the app settling
/// who is signed in, the profile gate's first read — and where Chat's model is working.
///
/// The bars build one after another, hold as the whole block, and settle back, on a loop: the same
/// gesture as the opening, so waiting looks like the app and not like the system. A custom control
/// because `ProgressView` draws only the system's own spinner. With Reduce Motion the mark stays put
/// and breathes in opacity instead.
struct MarkLoadingIndicator: View {
    /// The mark's height.
    var height: CGFloat = 36

    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    /// One build, hold and settle, in seconds.
    private static let period = 1.6

    var body: some View {
        TimelineView(.animation) { context in
            let phase = context.date.timeIntervalSinceReferenceDate.truncatingRemainder(dividingBy: Self.period) / Self.period
            BrandMark(rises: reduceMotion ? [1, 1, 1] : Self.rises(at: phase))
                .opacity(reduceMotion ? 0.55 + 0.45 * Self.breath(at: phase) : 1)
        }
        .frame(height: height)
        .accessibilityElement()
        .accessibilityLabel("Loading")
    }

    /// Each bar's height through one period: rising in turn over the first half, the block held, then
    /// all three settling to a stub together so the loop restarts from the baseline.
    private static func rises(at phase: Double) -> [Double] {
        let floor = 0.12
        return (0..<3).map { index in
            let start = Double(index) * 0.12
            let grown = ease(min(max((phase - start) / 0.26, 0), 1))
            let settled = ease(min(max((phase - 0.82) / 0.18, 0), 1))
            return floor + (1 - floor) * grown * (1 - settled)
        }
    }

    private static func breath(at phase: Double) -> Double {
        (1 - cos(phase * 2 * .pi)) / 2
    }

    private static func ease(_ t: Double) -> Double {
        t * t * (3 - 2 * t)
    }
}
