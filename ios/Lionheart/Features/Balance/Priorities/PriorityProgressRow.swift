import SwiftUI

/// One family's priority lift and how its week reads: the lift, the verdict with the estimated
/// singles behind it, and how it came to be the priority.
struct PriorityProgressRow: View {
    let family: PatternFamily
    let lift: PriorityLift
    var showsSparkline = false

    var body: some View {
        switch lift {
        case .none:
            VStack(alignment: .leading, spacing: 2) {
                Text(family.name).font(.headline)
                Text("No priority pinned, and no compound \(family.name.lowercased()) lift in 8 weeks.")
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        case .chosen(let chosen):
            chosenRow(chosen)
        case .unrecognised:
            Text("\(family.name): not readable by this version.")
                .foregroundStyle(Color(.secondaryLabel))
        }
    }

    private func chosenRow(_ chosen: ChosenPriority) -> some View {
        let standing = LiftStanding.of(chosen.progress)
        // The run of estimated singles sits under the figures, full width, so it has room to show a
        // twelve-week shape and the figures keep theirs.
        return VStack(alignment: .leading, spacing: 3) {
            Text("\(family.name) · \(chosen.name)")
                .font(.headline)
                .lineLimit(2)
            Text(Self.reading(standing, onSystemLoad: chosen.progress.isOnSystemLoad))
                .font(.subheadline.monospacedDigit())
                .foregroundStyle(standing.isProgress ? Color(.label) : Color(.secondaryLabel))
            if showsSparkline, chosen.series.count > 1 {
                Sparkline(points: chosen.series.map { MetricPoint(day: $0.day, value: $0.estimatedOneRepMax) })
                    .frame(height: 30)
                    .padding(.vertical, 2)
            }
            Text(choice(chosen))
                .font(.caption)
                .foregroundStyle(Color(.secondaryLabel))
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }

    /// "12-week best · ≈ 347 lb, from 321 (+8 %)".
    static func reading(_ standing: LiftStanding, onSystemLoad: Bool) -> String {
        let load = onSystemLoad ? " on system load" : ""
        switch standing {
        case .notTrained, .noEstimate, .unreadable:
            return standing.verdict
        case .firstBack(let best, let unit):
            return "\(standing.verdict) · \(Formatting.estimate(best, unit))\(load)"
        case .newBest(let best, let prior, let change, _, let unit),
             .held(let best, let prior, let change, let unit),
             .below(let best, let prior, let change, let unit):
            return "\(standing.verdict) · \(Formatting.estimate(best, unit)), from \(Formatting.estimateFigure(prior))"
                + " (\(Formatting.change(change * 100)) %)\(load)"
        }
    }

    func choice(_ chosen: ChosenPriority) -> String {
        let frequency = "\(Formatting.count(chosen.sessionsLastFourWeeks, "session")) in 4 weeks"
        switch chosen.choice {
        case .pinned(let pin):
            return "Pinned to \(Formatting.date(pin.horizon.ends)), \(pin.sessionsPerWeek) a week asked · \(frequency)"
        case .mostTrained:
            return "Not pinned: the most-trained \(family.name.lowercased()) lift of 8 weeks · \(frequency)"
        case .unrecognised:
            return frequency
        }
    }
}

extension LiftStanding {
    /// Whether the week moved the lift forward — what the row draws in the primary colour.
    var isProgress: Bool {
        if case .newBest = self { return true }
        return false
    }
}

extension LiftProgress {
    var isOnSystemLoad: Bool {
        switch self {
        case .firstBack(_, _, let onSystemLoad), .compared(_, _, _, _, let onSystemLoad): onSystemLoad
        case .notTrained, .noEstimate, .unrecognised: false
        }
    }
}
