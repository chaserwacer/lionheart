import SwiftUI

/// How to read Today's recovery section, opened from its header.
struct RecoveryExplanationSheet: View {
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            List {
                Section {
                    Text("Oura's contributors are scores out of 100: how Oura rated each against your own baseline. They are not measurements — a resting heart rate score of 80 is not 80 bpm. Each is drawn as a mark on a track, with the band you usually score in shaded and your mean as a tick.")
                    Text("Under each score is what the ring measured that the score rates, against your own 28 days before. Orange marks a figure outside your usual spread in the worse direction.")
                } header: {
                    Text("Scores and measurements")
                }
                Section {
                    explanation("HRV balance", "Last night's average HRV.")
                    explanation("Resting heart rate", "Last night's lowest heart rate.")
                    explanation("Recovery index", "When in the night the heart rate bottomed out. Earlier is better: a low point near waking usually means late training, alcohol or a late meal.")
                    explanation("Sleep balance", "Your sleep over the last 14 nights, asleep and in bed. Oura rates it against a sleep need it does not publish, so no need is shown.")
                    explanation("Previous night", "Last night's time asleep.")
                    explanation("Body temperature", "Oura's overnight temperature deviation from your baseline, in °C.")
                } header: {
                    Text("What each rests on")
                }
                Section {
                    Text("Readiness and the sleep score are Oura's scores too. The other figures are measurements.")
                }
            }
            .listStyle(.insetGrouped)
            .navigationTitle("Recovery")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Done") { dismiss() }
                }
            }
        }
    }

    private func explanation(_ name: String, _ text: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(name).font(.subheadline.weight(.semibold))
            Text(text).font(.subheadline).foregroundStyle(Color(.secondaryLabel))
        }
        .accessibilityElement(children: .combine)
    }
}
