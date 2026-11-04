import SwiftUI

/// What the log says comes next, with its reason: the family trained longest ago, its last day and
/// its week's hard sets, and Oura's readiness beside it only when it is under his usual range.
///
/// A statement, never a prompt: no button, nothing to dismiss, and nothing about missed days.
struct UpNextRow: View {
    let upNext: UpNext
    let families: [FamilyVolume]
    var today: CalendarDay = CalendarDay(Date())

    var body: some View {
        switch upNext {
        case .train(let due, let lastTrained, let trainedToday, let readiness):
            VStack(alignment: .leading, spacing: 4) {
                Text(trainedToday ? "Next session" : "Today")
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
                Text(PatternFamily.listed(due).capitalisingFirst)
                    .font(.title3.weight(.semibold))
                Text(reason(due, lastTrained: lastTrained))
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
                if case .low(let ouraScore, let usualLow, let usualHigh) = readiness {
                    Text("Oura's readiness score is \(ouraScore.rounded), under your 90-day range of \(usualLow.rounded)–\(usualHigh.rounded).")
                        .font(.subheadline)
                        .foregroundStyle(.orange)
                }
            }
            .padding(.vertical, 2)
            .accessibilityElement(children: .combine)
        case .nothingLogged:
            Text("No classified lifting logged yet, so there is no order to read.")
                .foregroundStyle(Color(.secondaryLabel))
        case .unrecognised:
            Text("Not readable by this version.")
                .foregroundStyle(Color(.secondaryLabel))
        }
    }

    /// "Last trained Tue 28 Oct. Push 4 of 8 and pull 3 of 8 hard sets in the last 7 days."
    private func reason(_ due: [PatternFamily], lastTrained: CalendarDay?) -> String {
        let when = lastTrained.map { "Last trained \(Formatting.dayInline($0, today: today))." } ?? "Never trained."
        let sets = due.compactMap { family in families.first { $0.family == family } }
            .map { "\($0.family.name.lowercased()) \($0.hardSets) of \($0.target)" }
        guard !sets.isEmpty else { return when }
        let listed = sets.count > 1 ? sets.dropLast().joined(separator: ", ") + " and " + sets[sets.count - 1] : sets[0]
        return "\(when) \(listed.capitalisingFirst) hard sets in the last 7 days."
    }
}

extension String {
    /// The first letter upper case, the rest as written.
    var capitalisingFirst: String {
        guard let first else { return self }
        return first.uppercased() + dropFirst()
    }
}
