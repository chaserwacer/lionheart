import SwiftUI

/// A horizontal bar whose ends lean forward at the mark's angle, for the score's partition: the
/// cells read as the mark's bars laid on their side.
///
/// A clip shape for a chart's `BarMark`, so the chart still places and sizes every cell; only the
/// ends change. A cell too narrow to lean keeps what lean it can rather than inverting.
struct LeanedBar: Shape {
    func path(in rect: CGRect) -> Path {
        let lean = min(rect.height * ProgressionMark.lean, rect.width / 2)
        var path = Path()
        path.move(to: CGPoint(x: rect.minX, y: rect.maxY))
        path.addLine(to: CGPoint(x: rect.maxX - lean, y: rect.maxY))
        path.addLine(to: CGPoint(x: rect.maxX, y: rect.minY))
        path.addLine(to: CGPoint(x: rect.minX + lean, y: rect.minY))
        path.closeSubpath()
        return path
    }
}
