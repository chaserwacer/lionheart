import Charts
import SwiftUI

/// One injury and everything logged against it, newest first.
///
/// The pain chart is the reading that matters: a tracked problem is a trajectory, and a list of
/// numbers makes the reader do the comparison themselves. Oldest-first in the chart, newest-first
/// in the list — the order each is actually read in.
struct InjuryDetailView: View {
    let history: InjuryHistory
    let store: BodyStore

    @Environment(\.dismiss) private var dismiss
    @State private var asked: InjuryAction?
    @State private var deleting: InjuryEvent?

    var body: some View {
        List {
            Section {
                LabeledContent("Since") { Text(Formatting.date(history.injury.injuryDate)) }
                LabeledContent("State") {
                    Text(history.injury.isActive ? "Active" : "Resolved")
                }
                if let pain = history.latestPain {
                    LabeledContent("Latest pain") {
                        Text("\(pain)/10").monospacedDigit()
                    }
                }
            }

            if let notes = history.injury.notes.nonEmpty {
                Section("Notes") { Text(notes) }
            }

            painSection
            eventsSection
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle(history.injury.name.nonEmpty ?? "Injury")
        .navigationBarTitleDisplayMode(.inline)
        .refreshable { await store.refresh() }
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Menu("More", systemImage: "ellipsis.circle") {
                    ForEach(InjuryAction.offered(on: history.injury)) { action in
                        Button(role: action.isDestructive ? .destructive : nil) {
                            if action.confirmation == nil { carryOut(action) } else { asked = action }
                        } label: {
                            Label(action.label, systemImage: action.systemImage)
                        }
                    }
                }
                .disabled(store.action.isRunning)
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
            if let confirmation = action.confirmation { Text(confirmation.warning) }
        }
        // An event is a treatment or a check-in the athlete logged on a day they cannot log again.
        .confirmationDialog(
            "Delete this event?",
            isPresented: Binding(get: { deleting != nil }, set: { if !$0 { deleting = nil } }),
            titleVisibility: .visible,
            presenting: deleting
        ) { event in
            Button("Delete event", role: .destructive) {
                let event = event
                deleting = nil
                Task { await store.delete(event) }
            }
        } message: { event in
            // Named, because the dialog points at the list rather than at the row it is about.
            Text("\(Self.label(of: event)) · The injury and its other events are kept. This cannot be undone.")
        }
    }

    /// Resolving stays on the screen — the state row above redraws and says so. Deleting leaves,
    /// because there is no longer an injury for this screen to be about.
    private func carryOut(_ action: InjuryAction) {
        asked = nil
        Task {
            switch action {
            case .resolve: await store.setTracking(of: history.injury, to: false)
            case .reopen: await store.setTracking(of: history.injury, to: true)
            case .delete:
                await store.delete(history.injury)
                if store.action.refusal == nil { dismiss() }
            }
        }
    }

    /// One event as a line of text: what it was, when, how much it hurt, and whatever was written
    /// about it — which is the part worth having on a pasteboard, for a physio or a note.
    private static func summary(of event: InjuryEvent) -> String {
        guard let notes = event.notes.nonEmpty else { return label(of: event) }
        return "\(label(of: event)) · \(notes)"
    }

    /// One event in a few words — what it was, how much it hurt, and when.
    private static func label(of event: InjuryEvent) -> String {
        var parts = ["\(InjuryEventRow.kind(of: event)), pain \(event.painLevel)/10"]
        if let createdAt = event.createdAt {
            parts.append(createdAt.formatted(.dateTime.month(.abbreviated).day().year()))
        }
        return parts.joined(separator: " · ")
    }

    @ViewBuilder
    private var painSection: some View {
        let dated = history.events.compactMap { event in
            event.createdAt.map { (event: event, at: $0) }
        }
        if dated.count >= 2 {
            Section {
                Chart(dated.sorted { $0.at < $1.at }, id: \.event.injuryEventId) { point in
                    LineMark(
                        x: .value("Logged", point.at),
                        y: .value("Pain", point.event.painLevel))
                    .foregroundStyle(Tokens.accent)
                    PointMark(
                        x: .value("Logged", point.at),
                        y: .value("Pain", point.event.painLevel))
                    .foregroundStyle(Tokens.accent)
                }
                .chartYScale(domain: 0...10)
                .frame(height: 160)
                .padding(.vertical, 8)
                .accessibilityLabel("Pain level over time, nought to ten")
            } header: {
                Text("Pain")
            } footer: {
                Text("Only events carrying a date are plotted.")
            }
        }
    }

    @ViewBuilder
    private var eventsSection: some View {
        Section("Events") {
            if history.events.isEmpty {
                Text("Nothing logged against this yet.")
                    .foregroundStyle(Color(.secondaryLabel))
            } else {
                ForEach(history.events) { event in
                    InjuryEventRow(event: event)
                        .contextMenu {
                            Button {
                                Clipboard.copy(Self.summary(of: event))
                            } label: {
                                Label("Copy", systemImage: "doc.on.doc")
                            }
                            Button(role: .destructive) {
                                deleting = event
                            } label: {
                                Label("Delete event", systemImage: "trash")
                            }
                        }
                        .swipeActions(edge: .trailing) {
                            Button(role: .destructive) {
                                deleting = event
                            } label: {
                                Label("Delete", systemImage: "trash")
                            }
                        }
                }
            }
        }
    }
}

/// One treatment or check-in.
private struct InjuryEventRow: View {
    let event: InjuryEvent

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            LabeledContent {
                Text("\(event.painLevel)/10")
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            } label: {
                Text(Self.kind(of: event))
                    .font(.subheadline)
            }
            if let createdAt = event.createdAt {
                Text(createdAt.formatted(.dateTime.month(.abbreviated).day().year()))
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
            }
            if let notes = event.notes.nonEmpty {
                Text(notes)
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }

    /// The event's kind in the athlete's words. Static because the copied line needs it too, and a
    /// row's spelling of a domain word and a pasteboard's must not drift apart.
    static func kind(of event: InjuryEvent) -> String {
        switch event.kind {
        case .treatment: "Treatment"
        case .checkIn: "Check-in"
        case .unrecognised: "Event"
        }
    }
}
