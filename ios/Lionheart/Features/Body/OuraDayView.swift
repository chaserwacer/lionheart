import SwiftUI

/// One Oura day, panel by panel.
///
/// The sections come from `OuraPanel.panels(of:)` rather than being written out, so a day with
/// sleep and no readiness simply has one section fewer — and so that the Oura surface widening in
/// parallel lands as a new case and a new section rather than as a rewrite of this screen.
struct OuraDayView: View {
    let day: OuraDay

    var body: some View {
        List {
            let panels = OuraPanel.panels(of: day)

            if panels.isEmpty {
                ContentUnavailableView {
                    Label("Nothing on this day", systemImage: "moon.zzz")
                } description: {
                    Text("The day is stored but carries no panels — the ring synced without data for it.")
                }
                .listRowBackground(Color.clear)
            } else {
                ForEach(panels) { panel in
                    Section(panel.title) {
                        if let score = panel.score, score > 0 {
                            LabeledContent("Score") {
                                Text("\(score)").monospacedDigit()
                            }
                        }
                        ForEach(panel.figures) { figure in
                            LabeledContent(figure.label) {
                                Text(figure.value).monospacedDigit()
                            }
                        }
                    }
                }
            }
        }
        .listStyle(.insetGrouped)
        .navigationTitle(Formatting.date(day.date))
        .navigationBarTitleDisplayMode(.inline)
    }
}
