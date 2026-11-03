import SwiftUI

/// Picking which conversation to continue.
///
/// A sheet rather than a sidebar: on a phone the transcript wants the whole screen, and switching
/// conversations is something done occasionally rather than while reading.
struct ConversationListSheet: View {
    let store: CoachStore

    @Environment(\.dismiss) private var dismiss
    @State private var pendingDeletion: ChatConversation?

    var body: some View {
        NavigationStack {
            List {
                if store.conversations.isEmpty {
                    ContentUnavailableView {
                        Label("No conversations yet", systemImage: "bubble.left.and.text.bubble.right")
                    } description: {
                        Text("Asking the coach something starts one.")
                    }
                    .listRowBackground(Color.clear)
                } else {
                    ForEach(sorted) { conversation in
                        Button {
                            open(conversation)
                        } label: {
                            ConversationRow(
                                conversation: conversation,
                                isOpen: conversation.chatConversationId
                                    == store.state.conversation?.chatConversationId)
                        }
                        .swipeActions {
                            Button("Delete", systemImage: "trash", role: .destructive) {
                                pendingDeletion = conversation
                            }
                        }
                    }
                }
            }
            .listStyle(.insetGrouped)
            .navigationTitle("Conversations")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Done") { dismiss() }
                }
            }
            // A deleted conversation cannot be recovered, and the swipe is easy to hit by accident
            // on a list you are scrolling.
            .confirmationDialog(
                "Delete this conversation?",
                isPresented: .init(
                    get: { pendingDeletion != nil },
                    set: { if !$0 { pendingDeletion = nil } }),
                presenting: pendingDeletion
            ) { conversation in
                Button("Delete", role: .destructive) {
                    let id = conversation.chatConversationId
                    pendingDeletion = nil
                    Task { await store.deleteConversation(id) }
                }
            } message: { conversation in
                Text("\(conversation.title) and everything in it. This cannot be undone.")
            }
        }
    }

    /// Newest activity first, which is the order you look for a conversation in. Sorted here
    /// rather than trusted from the route, because the list route's order is not part of its
    /// contract.
    private var sorted: [ChatConversation] {
        store.conversations.sorted {
            ($0.lastUpdate ?? .distantPast) > ($1.lastUpdate ?? .distantPast)
        }
    }

    private func open(_ conversation: ChatConversation) {
        dismiss()
        Task { await store.open(conversation.chatConversationId) }
    }
}

private struct ConversationRow: View {
    let conversation: ChatConversation
    let isOpen: Bool

    var body: some View {
        HStack {
            VStack(alignment: .leading, spacing: 3) {
                Text(conversation.title)
                    .font(.body)
                    .foregroundStyle(Color(.label))
                    .lineLimit(2)
                if let lastUpdate = conversation.lastUpdate {
                    Text(lastUpdate.formatted(.relative(presentation: .named)))
                        .font(.caption)
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
            Spacer()
            if isOpen {
                Image(systemName: "checkmark")
                    .foregroundStyle(Tokens.accent)
                    .accessibilityLabel("Open")
            }
        }
    }
}
