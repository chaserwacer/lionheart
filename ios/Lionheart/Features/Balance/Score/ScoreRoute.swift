import SwiftUI

/// Where the training score opens, as values on the stack that shows it.
///
/// Values rather than view-destination links: what the score opens goes on to open priorities and
/// movements by `HistoryRoute`, and a value pushed above a view-destination link lands on a blank
/// screen whose back button does nothing.
enum ScoreRoute: Hashable {
    case score
    case part(ScorePart)
    /// The last seven days' hard sets by pattern and muscle group.
    case volume
}

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
