import SwiftUI

/// One recorded scan, read the way the composition screen reads the latest, with its correction
/// and deletion.
struct ScanDetailView: View {
    let scan: BodyCompositionScan
    let store: BodyCompositionStore
    let bodyweight: BodyweightStore

    @Environment(\.dismiss) private var dismiss
    @State private var isCorrecting = false
    @State private var isConfirmingDelete = false

    var body: some View {
        List {
            ScanReadout(scan: scan, history: store.history)
            ScanPanels(sheet: scan.sheet)
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle(Formatting.date(scan.day))
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Menu("Scan actions", systemImage: "ellipsis") {
                    if scan.sheet.isWhollyUnderstood {
                        Button("Correct") { isCorrecting = true }
                    }
                    Button("Delete", role: .destructive) { isConfirmingDelete = true }
                }
            }
        }
        .sheet(isPresented: $isCorrecting) {
            ScanEntrySheet(store: store, bodyweight: bodyweight, correcting: scan)
        }
        .confirmationDialog("Delete this scan?", isPresented: $isConfirmingDelete, titleVisibility: .visible) {
            Button("Delete", role: .destructive) {
                Task {
                    await store.delete(scan)
                    if store.action.refusal == nil { dismiss() }
                }
            }
        } message: {
            Text("The bodyweight it logged stays logged.")
        }
    }
}
