import SwiftUI

/// Today's compact training score: the number, and the measurements it was read from in two lines,
/// opening onto the score's own screen.
///
/// Its own section and its own store, reached through the environment, so it sits in Today with one
/// line and nothing else on Today has to know it is there.
struct TrainingScoreSection: View {
    @Environment(TrainingBalanceStore.self) private var balance

    var body: some View {
        Section {
            switch balance.score {
            case .loading:
                SkeletonRows(count: 1)
            case .unavailable(let failure):
                Text(failure.message)
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
            case .loaded(let score), .stale(let score, _):
                NavigationLink {
                    TrainingScoreView()
                } label: {
                    TrainingScoreSummary(score: score)
                }
            }
        } header: {
            Text("Training score")
        }
        .task { await balance.refreshIfNeeded() }
    }
}

/// The score beside the figures behind it: push, pull and legs hard sets, zone 2, sessions.
struct TrainingScoreSummary: View {
    let score: TrainingScore

    var body: some View {
        HStack(alignment: .center, spacing: 14) {
            Text("\(score.value)")
                .font(.largeTitle.weight(.semibold))
                .monospacedDigit()
            VStack(alignment: .leading, spacing: 2) {
                Text(lifting)
                Text(cardioAndSessions)
            }
            .font(.footnote.monospacedDigit())
            .foregroundStyle(Color(.secondaryLabel))
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Training score \(score.value) of \(score.outOf). \(lifting). \(cardioAndSessions).")
    }

    private var lifting: String {
        score.lifting.families
            .map { "\($0.of.family.name) \($0.of.hardSets)/\($0.of.target)" }
            .joined(separator: " · ") + " sets"
    }

    private var cardioAndSessions: String {
        let cardio = switch score.cardio.measure {
        case .measured(let zone2, let target, let hard, let hardTarget, _, _):
            "Z2 \(Formatting.measure(zone2.rounded()))/\(target) · Z3–5 \(Formatting.measure(hard.rounded()))/\(hardTarget) min"
        case .unknown, .unrecognised: "Cardio unknown"
        }
        let sessions = score.consistency.blocks.first?.of.sessions ?? 0
        return "\(cardio) · \(Formatting.count(sessions, "session")) in 7 days"
    }
}
