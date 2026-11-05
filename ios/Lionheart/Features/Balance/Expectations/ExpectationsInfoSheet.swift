import SwiftUI

/// How the week's expectations and the adjusted score are worked out, in words. Opened from the info
/// button on the expectations section; the rules are the server's, restated.
struct ExpectationsInfoSheet: View {
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            List {
                Section {
                    Text("Every target on Train — the score's, the volume rows', what's next — is one answer from the server, with the reasons beside it.")
                }
                Section("Targets that follow you") {
                    Text("Hard sets, zone 2 and quality minutes grow from the four weeks before, plus 10 %, between a floor and the committed standard. Sessions hold at three a week.")
                }
                Section("Days you have") {
                    Text("Mark a day non-training — travel, a busy day — once or every week. Each target is then prorated to the days left in the seven and rounded up, so a short week still asks for its share. A session planned on a day counts it as a training day, over a weekday marked every week.")
                    Text("The weeks before a target keep their days off: after a week away the ramp back starts from what you did.")
                }
                Section("Two scores") {
                    Text("The training score always reads the full targets. The adjusted score reads the same arithmetic against the prorated ones, so a week of travel shows what the days you had allowed. With nothing marked off, the two are the same.")
                }
                Section("The next top set") {
                    Text("One step on last time's best set — 5 lb, or a rep where the load is bodyweight. It holds last time's set instead when today's going-in rating is under your usual day, the last top set was RPE 9.5 or more, you rated the last session of it 5 of 5 for difficulty, or the week's latest session 4 of 5 or more for fatigue. A pinned target is shown beside it.")
                }
            }
            .listStyle(.insetGrouped)
            .navigationTitle("This week's expectations")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Done") { dismiss() }
                }
            }
        }
        .presentationDetents([.medium, .large])
    }
}
