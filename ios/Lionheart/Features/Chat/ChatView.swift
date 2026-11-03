import SwiftUI

/// A standalone conversation: one the athlete starts, names by its first question, and comes back
/// to from the list — reached from the thread, which is the Chat tab's default.
///
/// It stays apart from the thread on purpose. Nothing said here is remembered, so it is where a
/// one-off question goes that should not shape later answers.
///
/// Not a `List`: a transcript is bubbles of varying width, bottom-anchored, and `List`'s row insets
/// and separators fight all three. The thing this screen has to get right is the **wait** — a reply
/// takes tens of seconds and there is no streaming route, so the question stays on screen, a working
/// row counts under it, and the composer says what is happening.
struct ChatView: View {
    let store: ChatStore

    @State private var typed = ""
    @State private var isContextCardPresented = false
    @State private var isConversationListPresented = false
    @FocusState private var isComposerFocused: Bool

    var body: some View {
        Group {
            switch store.state {
            case .loading:
                LoadingTranscript()
            case .unavailable(let failure):
                unavailable(failure)
            case .none:
                nothingAskedYet
            case .reading, .awaitingReply, .refused:
                transcript
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        // The grouped background every other tab gets from `List`. Without it the page is plain
        // white, and a model bubble — `secondarySystemGroupedBackground`, which *is* white in light
        // mode — disappears into it. The two colours are a pair.
        .background(Color(.systemGroupedBackground))
        .navigationTitle(store.state.conversation?.title ?? "Conversations")
        .navigationBarTitleDisplayMode(.inline)
        .toolbar { toolbar }
        .safeAreaInset(edge: .bottom) {
            ChatComposer(
                typed: $typed,
                isFocused: $isComposerFocused,
                acceptsTyping: store.state.acceptsTyping,
                isAwaitingReply: store.state.isAwaitingReply,
                onSend: send)
        }
        .sheet(isPresented: $isContextCardPresented) {
            ContextCardSheet(store: store)
        }
        .sheet(isPresented: $isConversationListPresented) {
            ConversationListSheet(store: store)
        }
        .task { await store.refresh() }
        // A refused exchange hands the question back, because the server did not keep it. Putting
        // it in the composer is what makes "try again" one tap rather than retyping it.
        .onChange(of: store.state.unsentText) { _, unsent in
            if let unsent, typed.isEmpty { typed = unsent }
        }
    }

    // MARK: - Transcript

    private var transcript: some View {
        ScrollView {
            LazyVStack(alignment: .leading, spacing: 14) {
                ForEach(store.state.turns) { turn in
                    TurnBubble(turn: turn)
                        .id(turn.id)
                }
                if store.state.isAwaitingReply {
                    WorkingRow()
                }
            }
            .padding(.horizontal, Tokens.cardPadding)
            .padding(.vertical, 16)
        }
        // A transcript is read from the bottom, and a new answer should already be in view when it
        // lands rather than needing a scroll to find.
        .defaultScrollAnchor(.bottom)
        .scrollDismissesKeyboard(.interactively)
    }

    // MARK: - Empty and not loaded

    private var nothingAskedYet: some View {
        ContentUnavailableView {
            Label("No conversations", systemImage: "bubble.left.and.text.bubble.right")
        } description: {
            Text("A conversation stands apart from the thread: nothing said in one is remembered.")
        } actions: {
            Button("View athlete context") { isContextCardPresented = true }
        }
    }

    private func unavailable(_ failure: LoadFailure) -> some View {
        ContentUnavailableView {
            Label("Chat unavailable", systemImage: "exclamationmark.triangle")
        } description: {
            switch failure {
            case .offline:
                Text("Chat needs a connection to the server, and the server is not reachable.")
            case .notFound, .refused, .unavailable:
                Text(failure.message)
            }
        } actions: {
            if failure.isWorthRetrying {
                Button("Try again") { Task { await store.refresh() } }
            }
        }
    }

    // MARK: - Chrome

    @ToolbarContentBuilder
    private var toolbar: some ToolbarContent {
        ToolbarItemGroup(placement: .topBarTrailing) {
            Button("All conversations", systemImage: "list.bullet") {
                isConversationListPresented = true
            }
            .disabled(store.state.isAwaitingReply)
            Button("New conversation", systemImage: "square.and.pencil") {
                store.startNewConversation()
                typed = ""
                isComposerFocused = true
            }
            .disabled(store.state.isAwaitingReply)
        }
    }

    private func send() {
        let words = typed
        typed = ""
        isComposerFocused = false
        Task { await store.send(words) }
    }
}
