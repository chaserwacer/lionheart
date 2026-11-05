import SwiftUI

/// One set already logged on the logging surface, with the three things that can be done to it.
///
/// A tap opens the edit sheet, because that is the gesture the athlete tries first. The long press
/// reaches the same sheet in a named way and adds the two actions a sheet is overkill for: logging
/// the same set again — three sets of five at the same weight is the commonest thing in a gym — and
/// deleting it.
struct LoggedSetRow: View {
    let logged: LoggedSet
    let number: Int
    let target: SetPrescription?
    let record: SetRecordOutcome
    let actions: Actions

    /// What can be done to a logged set. The surface owns the edit sheet, the delete confirmation
    /// and the entry row, so the row only asks.
    struct Actions {
        let edit: (LoggedSet) -> Void
        /// Logs the same set again, exactly as the Log button would.
        let logAgain: (LoggedSet) -> Void
        /// Asks to delete it. The surface confirms first.
        let delete: (LoggedSet) -> Void
    }

    var body: some View {
        SetRow(number: number, command: logged.command, target: target, record: record)
            .contentShape(.rect)
            .onTapGesture { actions.edit(logged) }
            .contextMenu {
                Button {
                    actions.edit(logged)
                } label: {
                    Label("Edit set", systemImage: "square.and.pencil")
                }
                Button {
                    actions.logAgain(logged)
                } label: {
                    Label("Log it again", systemImage: "plus.square.on.square")
                }
                Button(role: .destructive) {
                    actions.delete(logged)
                } label: {
                    Label("Delete set", systemImage: "trash")
                }
            }
            .swipeActions(edge: .leading) {
                Button {
                    actions.edit(logged)
                } label: {
                    Label("Edit", systemImage: "square.and.pencil")
                }
                .tint(Tokens.accent)
            }
            .swipeActions(edge: .trailing) {
                Button(role: .destructive) {
                    actions.delete(logged)
                } label: {
                    Label("Delete", systemImage: "trash")
                }
            }
    }
}
