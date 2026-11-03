import SwiftUI

/// One pin in a line: its movement and where the horizon stands, then how often, how recently and
/// how far — for the head of History and the list of past pins.
struct FocusSummaryRow: View {
    let reading: FocusReading
    let name: String

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(alignment: .firstTextBaseline) {
                Text(name)
                    .font(.subheadline.weight(.semibold))
                    .lineLimit(2)
                Spacer(minLength: 8)
                Text(FocusWords.phase(reading.phase))
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
            Text(FocusWords.summary(reading))
                .font(.footnote.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
        }
        .padding(.vertical, 1)
        .accessibilityElement(children: .combine)
    }
}
