import SwiftUI

/// Tempo's mark, "Progression": three forward-leaning bars, each a step taller — a training block's
/// weeks, the last one this week.
///
/// One geometry for every place the mark appears: the opening, the loading indicator, Settings,
/// Chat, the widgets, and the icon and launch bitmaps `Scripts/render-brand-assets.sh` draws from
/// this file. It is a `Shape` so it scales without a bitmap and animates: `bars` picks which bars to
/// draw, so the ink pair and the brick bar can be filled apart, and `rise` grows them from the
/// baseline. Whatever is drawn, the frame is the whole mark's, so separately filled bars stack into
/// one mark.
///
/// Pure SwiftUI geometry, with no UIKit, because the render script compiles it on macOS.
struct ProgressionMark: Shape {
    enum Bar: Int, CaseIterable, Sendable {
        case first, second, third
    }

    var bars: [Bar] = Bar.allCases
    /// How far the bars have grown from the baseline, 0 to 1. The lean holds as they grow, so a bar
    /// part-way up is the same parallelogram, shorter.
    var rise: Double = 1

    var animatableData: Double {
        get { rise }
        set { rise = newValue }
    }

    /// The mark's width over its height, for a frame that holds it with nothing to spare.
    static let aspectRatio = bounds.width / bounds.height

    /// How far a bar's top runs ahead of its foot, per unit of height: 7.6 over 36, 11.8 over 56 and
    /// 16 over 76 all lean by this to within half a percent. Anything that echoes the mark borrows it.
    static let lean = 0.2111

    func path(in rect: CGRect) -> Path {
        let scale = min(rect.width / Self.bounds.width, rect.height / Self.bounds.height)
        let origin = CGPoint(
            x: rect.midX - Self.bounds.width * scale / 2,
            y: rect.midY - Self.bounds.height * scale / 2)
        let rise = min(max(rise, 0), 1)
        func point(_ x: Double, _ y: Double) -> CGPoint {
            CGPoint(x: origin.x + (x - Self.bounds.minX) * scale, y: origin.y + (y - Self.bounds.minY) * scale)
        }
        var path = Path()
        guard rise > 0 else { return path }
        for bar in bars {
            let geometry = Self.geometry[bar.rawValue]
            let topY = Self.baseline - (Self.baseline - geometry.top) * rise
            let lift = (geometry.topLeft - geometry.footLeft) * rise
            path.move(to: point(geometry.footLeft, Self.baseline))
            path.addLine(to: point(geometry.footLeft + Self.width, Self.baseline))
            path.addLine(to: point(geometry.footLeft + Self.width + lift, topY))
            path.addLine(to: point(geometry.footLeft + lift, topY))
            path.closeSubpath()
        }
        return path
    }

    /// One bar in the approved 100-point box: where its foot starts, where its top starts, and how
    /// high it reaches. Every bar is 16 wide and stands on the same baseline.
    private struct BarGeometry {
        let footLeft: Double
        let topLeft: Double
        let top: Double
    }

    private static let baseline = 84.0
    private static let width = 16.0

    private static let geometry = [
        BarGeometry(footLeft: 14, topLeft: 21.6, top: 48),
        BarGeometry(footLeft: 40, topLeft: 51.8, top: 28),
        BarGeometry(footLeft: 66, topLeft: 82, top: 8),
    ]

    /// The box the three bars fill, inside the 100-point design box.
    private static let bounds = CGRect(x: 14, y: 8, width: 84, height: 76)
}
