import SwiftUI

/// Logging a wellness state: the athlete's own rating of a day — energy, mood, motivation and
/// stress, each 1 to 5.
///
/// A wellness state is his account of how he is, which Oura cannot measure and his training does
/// not carry; set beside readiness it shows whether his sense of recovery and the ring's agree.
/// Each rating starts unchosen, so nothing is logged that he did not pick. A day already rated
/// opens on its ratings, and saving replaces them — the server keeps one per day.
///
/// The sheet stays up until the server has the entry, and states a refusal in place, as the new
/// injury sheet does.
struct WellnessEntrySheet: View {
    let store: WellnessStore
    /// Entries already logged, to open a rated day on its ratings.
    let existing: [WellnessState]

    @Environment(\.dismiss) private var dismiss
    @State private var draft: WellnessDraft
    @State private var isSaving = false
    @State private var refusal: LoadFailure?

    init(store: WellnessStore, existing: [WellnessState], day: CalendarDay = CalendarDay(Date())) {
        self.store = store
        self.existing = existing
        _draft = State(initialValue: WellnessDraft.opening(on: day, existing: existing))
    }

    var body: some View {
        let today = CalendarDay(Date())
        NavigationStack {
            Form {
                Section {
                    DatePicker(
                        "Day",
                        selection: Binding(
                            get: { draft.day.middayDate() },
                            set: { draft = WellnessDraft.opening(on: CalendarDay($0), existing: existing) }),
                        in: ...Date(),
                        displayedComponents: .date)
                } footer: {
                    if existing.contains(where: { $0.date == draft.day }) {
                        Text("This day is already rated. Saving replaces its ratings.")
                    }
                }
                ForEach(WellnessDraft.Rating.allCases) { rating in
                    Section {
                        Picker(rating.name, selection: Binding(
                            get: { draft[rating] },
                            set: { draft[rating] = $0 }
                        )) {
                            ForEach(WellnessDraft.scale, id: \.self) { value in
                                Text("\(value)").tag(Optional(value))
                            }
                        }
                        .pickerStyle(.segmented)
                    } header: {
                        Text(rating.name)
                    } footer: {
                        HStack {
                            Text(rating.ends.low)
                            Spacer()
                            Text(rating.ends.high)
                        }
                    }
                }
                if let refusal {
                    Section {
                        Text(isOffline(refusal)
                             ? "Not saved: the server could not be reached. The ratings are still here."
                             : "Not saved. \(refusal.message)")
                            .foregroundStyle(.orange)
                    }
                }
            }
            .navigationTitle("Wellness")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    if isSaving {
                        ProgressView()
                    } else {
                        Button("Save") { save(today: today) }
                            .disabled(draft.entry(today: today) == nil)
                    }
                }
            }
            .interactiveDismissDisabled(isSaving)
        }
    }

    private func isOffline(_ failure: LoadFailure) -> Bool {
        if case .offline = failure { return true }
        return false
    }

    private func save(today: CalendarDay) {
        guard let entry = draft.entry(today: today) else { return }
        isSaving = true
        refusal = nil
        Task {
            defer { isSaving = false }
            do {
                try await store.log(entry)
                dismiss()
            } catch let failure as LoadFailure {
                refusal = failure
            } catch {
                // Cancelled: the sheet is already going away.
            }
        }
    }
}
