import SwiftUI

/// A span's headline figures, side by side as columns, each a caption over its figure — or one
/// labelled row each when the columns would not fit, as at the accessibility sizes, so no figure
/// truncates. Shared by every reading of the log that leads with figures over a chart.
struct SummaryFigures: View {
    let figures: [(label: String, value: String)]

    var body: some View {
        ViewThatFits(in: .horizontal) {
            HStack(alignment: .top, spacing: 20) {
                ForEach(figures, id: \.label) { figure in
                    VStack(alignment: .leading, spacing: 2) {
                        Text(figure.label)
                            .font(.caption)
                            .foregroundStyle(Color(.secondaryLabel))
                        Text(figure.value)
                            .font(.title3.weight(.semibold).monospacedDigit())
                    }
                    .fixedSize()
                }
            }
            VStack(alignment: .leading, spacing: 4) {
                ForEach(figures, id: \.label) { figure in
                    LabeledContent(figure.label) {
                        Text(figure.value).monospacedDigit()
                    }
                }
            }
        }
        .accessibilityElement(children: .combine)
    }
}
