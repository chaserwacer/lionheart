import SwiftUI

/// The logging surface's one line about how the session is going: the ratings given so far, and a
/// tap into the sheet that sets them.
///
/// At the foot of the list, out of the way of the sets: it is used once or twice a session, and
/// logging the next set is what the surface is for.
struct SessionEffortRow: View {
    let effort: PerceivedEffortRequest
    let open: () -> Void

    var body: some View {
        Section {
            Button(action: open) {
                LabeledContent {
                    ViewThatFits(in: .horizontal) {
                        Text(summary(\.shortLabel))
                        Text(count)
                    }
                    .monospacedDigit()
                } label: {
                    Text("How it's going").foregroundStyle(Color(.label))
                }
            }
            .accessibilityLabel("How it's going")
            .accessibilityValue(effort.isEmpty ? "Not rated" : summary(\.label))
            .accessibilityHint("Rates the session so far")
        }
    }

    /// "Difficulty 4 · Engagement 5", or "Not rated".
    private func summary(_ name: KeyPath<EffortScale, String>) -> String {
        let rated = effort.ratedScales
        guard !rated.isEmpty else { return "Not rated" }
        return rated.compactMap { scale in effort[scale].map { "\(scale[keyPath: name]) \($0)" } }
            .joined(separator: " · ")
    }

    /// "2 of 4 rated", where the names do not fit.
    private var count: String {
        let rated = effort.ratedScales.count
        return rated == 0 ? "Not rated" : "\(rated) of \(EffortScale.allCases.count) rated"
    }
}
