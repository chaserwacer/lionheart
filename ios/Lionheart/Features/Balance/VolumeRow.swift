import SwiftUI

/// One line of weekly volume: what it is and when it was last trained, its hard sets against the
/// target, and the bar between them.
///
/// The same row for a family, a pattern and a muscle group, so the three lists read as one
/// instrument at three resolutions.
struct VolumeRow: View {
    let label: String
    let sets: Double
    /// Absent for work that is counted but not judged — isolation as a pattern.
    let target: Int?
    let lastTrained: CalendarDay?
    var today: CalendarDay = CalendarDay(Date())

    var body: some View {
        VStack(alignment: .leading, spacing: 5) {
            HStack(alignment: .firstTextBaseline, spacing: 6) {
                Text(label)
                Text(last)
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
                Spacer(minLength: 8)
                Text(figure)
                    .monospacedDigit()
                    .foregroundStyle(Color(.secondaryLabel))
            }
            if let target {
                TargetBar(value: sets, target: Double(target))
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(label)
        .accessibilityValue("\(figure) hard sets. Last trained \(last).")
    }

    private var figure: String {
        let measured = Formatting.measure(sets)
        guard let target else { return measured }
        return "\(measured) of \(target)"
    }

    /// When it was last trained, short enough to sit beside the name: "6 days ago", "never".
    private var last: String {
        lastTrained.map { Formatting.dayInline($0, today: today) } ?? "never"
    }
}
