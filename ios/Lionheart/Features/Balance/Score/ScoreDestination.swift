import SwiftUI

/// The screen for each `ScoreRoute`, registered by every stack that shows the score.
struct ScoreDestination: View {
    let route: ScoreRoute
    /// Opens a part from a tap on the score's partition, by appending to the stack's path.
    let open: (ScorePart) -> Void

    @Environment(TrainingBalanceStore.self) private var balance

    var body: some View {
        switch route {
        case .score:
            TrainingScoreView(open: open)
        case .part(let part):
            ScorePartView(part: part)
        case .volume:
            if let volume = balance.volume.value {
                VolumeView(volume: volume)
            } else {
                ContentUnavailableView("Volume not available", systemImage: "exclamationmark.triangle")
            }
        }
    }
}
