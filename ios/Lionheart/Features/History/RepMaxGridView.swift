import SwiftUI

/// The rep-max grid: one cell per rep count, one to twelve, in one weight unit.
///
/// A grid rather than twelve rows because it is read as a shape — where the curve is strong, which
/// counts are untested, which went up last time — and a list makes that a scroll. Each cell is a
/// stock bordered button, so it has the system's own fill, shape, touch target and pressed state,
/// and the cells set in the latest session that set any are the prominent ones.
struct RepMaxGridView: View {
    let grid: RepMaxGrid
    let name: String
    let ask: (PersonalRecord) -> Void
    /// Opens a cell's chain. A closure rather than a `NavigationLink`: inside a `List` row, a link
    /// is restyled as a disclosure row and the whole row becomes one tap target, so twelve links
    /// in one row would read as twelve chevrons and open whichever SwiftUI chose.
    let open: (RecordKey) -> Void

    /// Wide enough for a four-figure weight at the current text size, so the column count drops
    /// as Dynamic Type grows rather than the figures truncating.
    @ScaledMetric(relativeTo: .headline) private var cellWidth: CGFloat = 72

    var body: some View {
        LazyVGrid(columns: [GridItem(.adaptive(minimum: cellWidth), spacing: 8)], spacing: 8) {
            ForEach(grid.cells) { cell in
                if let record = cell.record {
                    held(cell, record)
                } else {
                    untested(cell)
                }
            }
        }
        .padding(.vertical, 6)
    }

    private func held(_ cell: RepMaxGrid.Cell, _ record: PersonalRecord) -> some View {
        Button { open(record.key) } label: { face(cell) }
        .modifier(CellProminence(isNewest: cell.isNewest))
        .contextMenu { RecordMenu(record: record, name: name, ask: ask) }
        .accessibilityLabel(
            "\(record.achievement.kind.label), \(Formatting.achievement(record.achievement))"
                + (cell.isNewest ? ", newest" : ""))
        .accessibilityHint(Formatting.recordLineage(record))
    }

    private func untested(_ cell: RepMaxGrid.Cell) -> some View {
        Button {} label: { face(cell) }
            .buttonStyle(.bordered)
            .disabled(true)
            .accessibilityLabel("\(RecordKind.repMax(reps: cell.reps).label), none yet")
    }

    private func face(_ cell: RepMaxGrid.Cell) -> some View {
        VStack(spacing: 2) {
            Text("\(cell.reps)RM")
                .font(.caption)
            Text(cell.weight.map(Formatting.measure) ?? "—")
                .font(.headline.monospacedDigit())
                .lineLimit(1)
                .minimumScaleFactor(0.8)
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, 4)
    }
}

/// Bordered for a standing rep max, prominent for one set in the latest session — two stock
/// styles, so the emphasis follows the accent and the appearance without a colour of its own.
private struct CellProminence: ViewModifier {
    let isNewest: Bool

    func body(content: Content) -> some View {
        if isNewest {
            content.buttonStyle(.borderedProminent)
        } else {
            content.buttonStyle(.bordered)
        }
    }
}
