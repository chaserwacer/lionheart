import Foundation

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
