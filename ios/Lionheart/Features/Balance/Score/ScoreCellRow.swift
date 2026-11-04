import SwiftUI

/// One measurement of the score: what it measured against its target, with the bar between them,
/// and — where it earns points — what it earned of what it could.
struct ScoreCellRow: View {
    let cell: ScoreCell

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            ViewThatFits(in: .horizontal) {
                HStack(alignment: .firstTextBaseline) {
                    Text(cell.label)
                    Spacer()
                    figures
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text(cell.label)
                    figures
                }
            }
            if case .quantity(let measured, let target, _) = cell.reading {
                TargetBar(value: measured, target: target)
            }
            if let basis = cell.basis {
                Text(basis)
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(cell.label)
        .accessibilityValue(spoken)
    }

    private var figures: some View {
        HStack(alignment: .firstTextBaseline, spacing: 8) {
            Text(cell.figure)
                .foregroundStyle(Color(.secondaryLabel))
            if cell.outOf > 0 {
                Text(ScorePoints.earned(cell.points, of: cell.outOf))
                    .fontWeight(.semibold)
            }
        }
        .monospacedDigit()
    }

    private var spoken: String {
        let basis = cell.basis.map { " Target from \($0)." } ?? ""
        guard cell.outOf > 0 else { return cell.figure + basis }
        return "\(cell.figure). \(ScorePoints.spoken(cell.points, of: cell.outOf)).\(basis)"
    }
}
