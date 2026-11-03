import SwiftUI

/// What a long press on a record offers: retraction where the record stands, and a copy.
struct RecordMenu: View {
    let record: PersonalRecord
    let name: String
    let ask: (PersonalRecord) -> Void

    var body: some View {
        if record.isRetractable {
            Button(role: .destructive) {
                ask(record)
            } label: {
                Label("Retract record", systemImage: "arrow.uturn.backward.circle")
            }
        }
        Button {
            Clipboard.copy("\(name) · \(record.achievement.kind.label) \(Formatting.achievement(record.achievement))")
        } label: {
            Label("Copy", systemImage: "doc.on.doc")
        }
    }
}

extension View {
    /// The confirmation in front of a retraction, hung once on the screen rather than per row — a
    /// dialog per row means the one that opens is whichever row SwiftUI happened to keep.
    func retractionDialog(
        asking asked: Binding<PersonalRecord?>, name: String, store: HistoryStore
    ) -> some View {
        confirmationDialog(
            asked.wrappedValue?.retractionConfirmation.question ?? "",
            isPresented: Binding(
                get: { asked.wrappedValue != nil },
                set: { if !$0 { asked.wrappedValue = nil } }),
            titleVisibility: .visible,
            presenting: asked.wrappedValue
        ) { record in
            Button(record.retractionConfirmation.proceed, role: .destructive) {
                asked.wrappedValue = nil
                Task { await store.retract(record) }
            }
        } message: { record in
            Text("\(name) · \(record.achievement.kind.label) \(Formatting.achievement(record.achievement)). \(record.retractionConfirmation.warning)")
        }
    }
}
