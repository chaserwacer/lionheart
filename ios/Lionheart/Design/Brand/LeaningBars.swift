import SwiftUI

/// A run of upright bars leaning forward at the mark's angle — weeks of a block, the parts of a
/// rating — each as tall as its share of the tallest, so the row reads as the mark drawn from his
/// own figures.
///
/// Colour says which bar is which, never decoration: the one bar that is now — this week, the part
/// to act on — in Brick; what came before in Iron; what is still ahead as an empty track. The bars
/// rise from the baseline in turn when they appear and spring to new heights when a figure changes,
/// the opening's gesture; with Reduce Motion they are simply there. A custom view because Charts
/// draws a bar as a rectangle, and the lean is the point.
struct LeaningBars: View {
    struct Bar: Identifiable, Equatable {
        let id: String
        /// Its height as a share of the row's, 0 to 1. Ignored for a bar still ahead.
        let height: Double
        let role: Role
    }

    enum Role: Equatable {
        /// Before now, for context.
        case before
        /// The one bar that is now.
        case now
        /// Not reached yet: a track with nothing in it.
        case ahead
        /// Reached, with nothing to measure — a week with no session on the lift.
        case empty
    }

    let bars: [Bar]
    var spacing: CGFloat = 3

    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var hasRisen = false

    /// A stub, so a bar with a figure never vanishes into the baseline beside a tall one.
    private static let floor = 0.06

    var body: some View {
        HStack(alignment: .bottom, spacing: spacing) {
            ForEach(Array(bars.enumerated()), id: \.element.id) { index, bar in
                column(bar)
                    .animation(reduceMotion ? nil : .brandRise(step: index), value: hasRisen)
                    .animation(reduceMotion ? nil : .brandRise(step: 0), value: bar.height)
            }
        }
        .accessibilityHidden(true)
        .onAppear { hasRisen = true }
    }

    @ViewBuilder
    private func column(_ bar: Bar) -> some View {
        switch bar.role {
        case .ahead:
            LeanedColumn(rise: 1)
                .fill(Color(.quaternarySystemFill))
        case .empty:
            LeanedColumn(rise: hasRisen ? Self.floor : 0)
                .fill(Color(.tertiarySystemFill))
        case .before, .now:
            LeanedColumn(rise: hasRisen ? max(Self.floor, min(1, bar.height)) : 0)
                .fill(bar.role == .now ? Tokens.accent : Tokens.iron)
        }
    }
}

/// One of `LeaningBars`'s bars: a parallelogram standing on the bottom of its frame, its top running
/// ahead of its foot by the mark's lean, risen to a share of the frame's height. Shorter is the same
/// angle, not a squashed bar, as the mark's bars grow in the opening.
struct LeanedColumn: Shape {
    var rise: Double

    var animatableData: Double {
        get { rise }
        set { rise = newValue }
    }

    func path(in rect: CGRect) -> Path {
        let height = rect.height * min(max(rise, 0), 1)
        var path = Path()
        guard height > 0 else { return path }
        // The foot leaves room for the full-height lean, so every bar in a row is the same width.
        let width = max(1, rect.width - rect.height * ProgressionMark.lean)
        let lean = height * ProgressionMark.lean
        path.move(to: CGPoint(x: rect.minX, y: rect.maxY))
        path.addLine(to: CGPoint(x: rect.minX + width, y: rect.maxY))
        path.addLine(to: CGPoint(x: rect.minX + width + lean, y: rect.maxY - height))
        path.addLine(to: CGPoint(x: rect.minX + lean, y: rect.maxY - height))
        path.closeSubpath()
        return path
    }
}

extension Animation {
    /// The opening's rise: a short spring with a little bounce, each bar a beat after the one
    /// before, starting `after` seconds in. Anything that grows a bar from the baseline uses it, so
    /// the motion is the brand's.
    static func brandRise(step: Int = 0, after: Double = 0) -> Animation {
        .spring(duration: 0.42, bounce: 0.14).delay(after + Double(step) * 0.05)
    }
}
