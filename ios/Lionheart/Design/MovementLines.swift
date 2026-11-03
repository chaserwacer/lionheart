import SwiftUI

/// A session's movements as log-book lines — `Barbell Squat   330 lb × 6, 5, 5, 4` — in a two-column
/// grid, so the loads line up down the right edge and seven movements are seven short lines.
///
/// A custom layout because no stock row aligns a column of figures across several lines inside one
/// list row. At the accessibility sizes each figure moves under its name instead of being squeezed
/// beside it. Shared by Today's training rows and History's log, so a session reads the same in
/// both.
struct MovementLines: View {
    let digests: [MovementDigest]

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    var body: some View {
        if dynamicTypeSize.isAccessibilitySize {
            VStack(alignment: .leading, spacing: 4) {
                ForEach(digests) { digest in
                    VStack(alignment: .leading, spacing: 0) {
                        Text(digest.movementName).font(.subheadline)
                        Text(Self.line(digest.line))
                            .font(.subheadline.monospacedDigit())
                            .foregroundStyle(Color(.secondaryLabel))
                    }
                }
            }
        } else {
            Grid(alignment: .leadingFirstTextBaseline, horizontalSpacing: 12, verticalSpacing: 3) {
                ForEach(digests) { digest in
                    GridRow {
                        // The name wraps before a figure truncates: the loads are the reason the
                        // line is read.
                        Text(digest.movementName)
                            .font(.subheadline)
                            .lineLimit(2)
                            .fixedSize(horizontal: false, vertical: true)
                            .frame(maxWidth: .infinity, alignment: .leading)
                        Text(Self.line(digest.line))
                            .font(.subheadline.monospacedDigit())
                            .foregroundStyle(Color(.secondaryLabel))
                            .fixedSize()
                            .gridColumnAlignment(.trailing)
                    }
                }
            }
        }
    }

    /// A movement's line as a lifter writes it.
    static func line(_ line: MovementDigest.Line) -> String {
        switch line {
        case .straight(let weight, let unit, let reps):
            let load = Formatting.weight(weight, unit)
            return "\(load) × \(reps.map(String.init).joined(separator: ", "))"
        case .ramped(let top, let setCount):
            let load = Formatting.weight(top.weight, top.unit)
            return "\(Formatting.count(setCount, "set")), top \(load) × \(top.reps)"
        case .counted(let setCount):
            return Formatting.count(setCount, "set")
        }
    }
}
