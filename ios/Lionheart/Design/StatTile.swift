import SwiftUI

/// One figure with its name under it — resting heart rate, a sleep score, a set count.
///
/// A custom view because SwiftUI has no stock pairing of a value and a caption at the sizes a row
/// of them needs. It stays inside the system's type scale, and the value carries
/// `.monospacedDigit()` so a figure that changes in place does not make the label beside it jump.
struct StatTile: View {
    let value: String
    let label: String

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(value)
                .font(.headline.monospacedDigit())
                .foregroundStyle(Color(.label))
            Text(label)
                .font(.caption)
                .foregroundStyle(Color(.secondaryLabel))
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(label): \(value)")
    }
}
