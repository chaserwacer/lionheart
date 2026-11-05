import SwiftUI

/// The score's parts as rows, each its points and its measurements in a line, opening the part,
/// and the weeks — the words under Train's hero.
struct TrainingScorePartsSection: View {
    let score: TrainingScore

    var body: some View {
        Section {
            ForEach(ScoreBreakdown.of(score)) { part in
                NavigationLink(value: ScoreRoute.part(part.part)) {
                    ScorePartRow(part: part)
                }
            }
            NavigationLink(value: ScoreRoute.score) {
                Text("Weeks")
            }
        } header: {
            Text("Score parts")
        }
    }
}
