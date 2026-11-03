import SwiftUI

/// One injury in a list: what it is, how it is going, and since when.
struct InjuryRow: View {
    let history: InjuryHistory
    let today: CalendarDay

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(InjuryRow.name(of: history))
                .font(.headline)
            Text(InjuryRow.subtitle(of: history))
                .font(.subheadline)
                .foregroundStyle(Color(.secondaryLabel))
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(InjuryRow.name(of: history)). \(InjuryRow.subtitle(of: history))")
    }

    static func name(of history: InjuryHistory) -> String {
        history.injury.name.nonEmpty ?? "Unnamed injury"
    }

    static func subtitle(of history: InjuryHistory) -> String {
        var parts = ["Since \(Formatting.date(history.injury.injuryDate))"]
        // Absent is not zero: nothing logged against it yet is a different thing from no pain.
        if let pain = history.latestPain { parts.append("last pain \(pain)/10") }
        if !history.injury.isActive { parts.append("resolved") }
        return parts.joined(separator: " · ")
    }
}

extension View {
    /// Hangs an injury's actions on the row that shows it.
    ///
    /// A modifier rather than something `InjuryRow` does for itself, because both screens put the
    /// row inside a `NavigationLink` and **`.swipeActions` belongs on the row, not on something
    /// drawn inside it** — applied to the link's label it is attached below the level `List` reads
    /// swipes from. It is one modifier so that the tab's "Being tracked" section and the full list
    /// cannot drift into offering different actions for the same injury.
    func injuryActions(on history: InjuryHistory, store: BodyStore) -> some View {
        modifier(InjuryActions(history: history, store: store))
    }
}

/// The long press, the two swipes and the dialog that guards the delete.
private struct InjuryActions: ViewModifier {
    let history: InjuryHistory
    let store: BodyStore

    @State private var asked: InjuryAction?

    func body(content: Content) -> some View {
        content
            .contextMenu { menu }
            // Resolving is the action reached for most and the one undone by a tap, so it gets the
            // leading swipe. Deleting keeps the trailing edge iOS reserves for it.
            .swipeActions(edge: .leading) {
                Button {
                    carryOut(direction)
                } label: {
                    Label(direction.label, systemImage: direction.systemImage)
                }
                .tint(history.injury.isActive ? .green : .orange)
            }
            .swipeActions(edge: .trailing) {
                Button(role: .destructive) {
                    asked = .delete
                } label: {
                    Label("Delete", systemImage: "trash")
                }
            }
            .confirmationDialog(
                asked?.confirmation?.question ?? "",
                isPresented: Binding(get: { asked != nil }, set: { if !$0 { asked = nil } }),
                titleVisibility: .visible,
                presenting: asked
            ) { action in
                if let confirmation = action.confirmation {
                    Button(confirmation.proceed, role: .destructive) { carryOut(action) }
                }
            } message: { action in
                if let confirmation = action.confirmation {
                    Text("\(InjuryRow.name(of: history)) · \(confirmation.warning)")
                }
            }
    }

    @ViewBuilder
    private var menu: some View {
        ForEach(InjuryAction.offered(on: history.injury)) { action in
            Button(role: action.isDestructive ? .destructive : nil) {
                if action.confirmation == nil { carryOut(action) } else { asked = action }
            } label: {
                Label(action.label, systemImage: action.systemImage)
            }
        }
        Button {
            Clipboard.copy("\(InjuryRow.name(of: history)) · \(InjuryRow.subtitle(of: history))")
        } label: {
            Label("Copy", systemImage: "doc.on.doc")
        }
    }

    /// The one direction this injury can move in: an active injury resolves, a resolved one is
    /// taken up again.
    private var direction: InjuryAction {
        history.injury.isActive ? .resolve : .reopen
    }

    private func carryOut(_ action: InjuryAction) {
        asked = nil
        Task {
            switch action {
            case .resolve: await store.setTracking(of: history.injury, to: false)
            case .reopen: await store.setTracking(of: history.injury, to: true)
            case .delete: await store.delete(history.injury)
            }
        }
    }
}
