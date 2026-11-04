import SwiftUI

/// What to train next, as Train opens with it: the next lift and the next cardio, the one that makes
/// more sense first on top and marked, each opening onto the reasons it was read from.
///
/// A recommendation, never an instruction: no button, nothing to dismiss, and every choice states the
/// facts behind it so the athlete can disagree. The server's one answer, so the workout tracker's
/// header and the chat say the same.
struct NextSessionsSection: View {
    @Environment(TrainingBalanceStore.self) private var balance

    var body: some View {
        switch balance.nextSessions {
        case .loading:
            Section("Up next") { SkeletonRows(count: 2) }
        case .unavailable(let failure):
            Section("Up next") {
                Text(failure.message).font(.footnote).foregroundStyle(Color(.secondaryLabel))
            }
        case .loaded(let next), .stale(let next, _):
            content(next)
        }
    }

    private func content(_ next: NextSessions) -> some View {
        let liftFirst = next.first.session != .cardio
        return Section {
            if liftFirst {
                LiftRow(lift: next.lift, isFirst: true)
                CardioRow(cardio: next.cardio, isFirst: false)
            } else {
                CardioRow(cardio: next.cardio, isFirst: true)
                LiftRow(lift: next.lift, isFirst: false)
            }
        } header: {
            Text("Up next")
        } footer: {
            if !next.first.reasons.isEmpty {
                Text("\(liftFirst ? "Lift" : "Cardio") first: \(next.first.reasons.joined(separator: " "))")
            }
        }
    }
}

/// The next lift: the family due, and each due family's priority with its next top set.
private struct LiftRow: View {
    let lift: NextLift
    let isFirst: Bool

    var body: some View {
        switch lift {
        case .train(let families, _, let priorities, let reasons):
            NextRow(
                kind: "Lift",
                title: PatternFamily.listed(families).capitalisingFirst,
                details: priorities.map(aim),
                reasons: reasons,
                isFirst: isFirst)
        case .nothingLogged(let reasons):
            NextRow(kind: "Lift", title: "Nothing classified yet", details: [], reasons: reasons, isFirst: isFirst)
        case .unrecognised:
            NextRow(kind: "Lift", title: "Not readable by this version", details: [], reasons: [], isFirst: isFirst)
        }
    }

    /// "Bench Press 5 × 205 lb", with the estimate it would make and the best it has to pass.
    private func aim(_ priority: PriorityAim) -> String {
        switch priority.aim {
        case .beat(let load, let reps, let unit, let estimate, let best, let onSystemLoad):
            let set = "\(reps) × \(Formatting.weight(load, unit))\(onSystemLoad ? " system load" : "")"
            return "\(priority.name) \(set) · e1RM \(Formatting.measure(estimate.rounded())) against \(Formatting.measure(best.rounded()))"
        case .establish:
            return "\(priority.name): work up to a top set of 5"
        case .unrecognised:
            return priority.name
        }
    }
}

/// The next cardio: zone 2 for its minutes, or intervals with their structure.
private struct CardioRow: View {
    let cardio: NextCardio
    let isFirst: Bool

    var body: some View {
        switch cardio {
        case .zone2(let minutes, let reasons):
            NextRow(
                kind: "Cardio", title: "Zone 2, \(minutes) min",
                details: ["Continuous at 60–70 % of maximum heart rate"], reasons: reasons, isFirst: isFirst)
        case .quality(let bouts, let boutMinutes, let recoveryMinutes, let reasons):
            NextRow(
                kind: "Cardio", title: "Intervals, \(bouts) × \(boutMinutes) min",
                details: ["Zone 4–5, \(recoveryMinutes) min easy between · \(bouts * boutMinutes) min of work"],
                reasons: reasons, isFirst: isFirst)
        case .unrecognised:
            NextRow(kind: "Cardio", title: "Not readable by this version", details: [], reasons: [], isFirst: isFirst)
        }
    }
}

/// One recommendation: what it is, its figures, and its reasons under a disclosure.
private struct NextRow: View {
    let kind: String
    let title: String
    let details: [String]
    let reasons: [String]
    let isFirst: Bool

    @State private var isExpanded = false

    var body: some View {
        DisclosureGroup(isExpanded: $isExpanded) {
            ForEach(reasons, id: \.self) { reason in
                Text(reason)
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        } label: {
            VStack(alignment: .leading, spacing: 3) {
                HStack(alignment: .firstTextBaseline) {
                    Text(kind)
                        .font(.caption)
                        .foregroundStyle(Color(.secondaryLabel))
                    if isFirst {
                        Text("First")
                            .font(.caption.weight(.semibold))
                            .foregroundStyle(.tint)
                    }
                }
                Text(title)
                    .font(isFirst ? .title3.weight(.semibold) : .headline)
                ForEach(details, id: \.self) { detail in
                    Text(detail)
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
            .padding(.vertical, 2)
            .accessibilityElement(children: .combine)
            .accessibilityLabel("\(kind)\(isFirst ? ", first" : ""): \(title)")
            .accessibilityValue(details.joined(separator: ". "))
            .accessibilityHint(reasons.isEmpty ? "" : "Shows the reasons")
        }
    }
}
