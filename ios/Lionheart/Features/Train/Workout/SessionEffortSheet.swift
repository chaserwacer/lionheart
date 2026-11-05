import SwiftUI

/// Rating the session while it is going: the four effort ratings and the notes, opened from the
/// logging surface whenever the athlete likes.
///
/// What is set here stays with the workout and comes back filled in on the finish sheet, so
/// finishing never asks twice. The ratings are written as they are picked (`EffortRatingRows`); the
/// notes are written when the sheet closes rather than on every keystroke, since each write also
/// redraws the Lock Screen's copy of the workout.
struct SessionEffortSheet: View {
    let store: ActiveWorkoutStore

    @Environment(\.dismiss) private var dismiss
    @State private var notes: String

    init(store: ActiveWorkoutStore) {
        self.store = store
        _notes = State(initialValue: store.workout?.notes ?? "")
    }

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    EffortRatingRows(store: store)
                } footer: {
                    Text("Kept with the workout. They come up again, filled in, when you finish.")
                }
                Section("Notes") {
                    TextField("Anything worth remembering", text: $notes, axis: .vertical)
                        .lineLimit(2...6)
                }
            }
            .navigationTitle("How it's going")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Done") { dismiss() }
                }
            }
        }
        .presentationDetents([.medium, .large])
        .onDisappear { store.note(notes) }
    }
}
