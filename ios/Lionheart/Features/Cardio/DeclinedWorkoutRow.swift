import SwiftUI

/// A ring workout the heart rate did not show as training: what the ring called it, when, and the
/// heart rate that decided it — so a declined game can be told from a declined game of catch.
struct DeclinedWorkoutRow: View {
    let declined: DeclinedWorkout

    var body: some View {
        VStack(alignment: .leading, spacing: 5) {
            HStack(alignment: .firstTextBaseline) {
                Text(declined.session.activityName).font(.subheadline.weight(.semibold))
                Spacer()
                Text(declined.session.start, format: .dateTime.month(.abbreviated).day().hour().minute())
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
            Text(facts)
                .font(.caption.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
            if case .recorded(_, _, _, let zones?, _) = declined.session.heartRate, zones.inZones > 0 {
                ZoneBar(zones: zones).frame(height: 6)
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }

    private var facts: String {
        var parts = [Formatting.minutes(declined.session.minutes)]
        if case .recorded(let average, let peak, _, let zones, _) = declined.session.heartRate {
            parts.append("avg \(average) · peak \(peak) bpm")
            if let zones {
                parts.append("Z2+ \(Formatting.minutes(zones.zone2 + zones.zone3 + zones.zone4 + zones.zone5))")
            }
        }
        parts.append(CardioWords.verdict(declined.reason))
        return parts.joined(separator: " · ")
    }
}
