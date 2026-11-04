import SwiftUI

/// What does it mean? — asked in one continuing thread.
///
/// The Chat tab opens here. The athlete never starts a conversation: he scrolls back through
/// everything he has said, and the server decides what the model reads — the current session, and
/// what earlier sessions left in memory. Each session's opening is the only seam in the transcript,
/// a dated line the way a messaging app marks a gap.
///
/// Standalone conversations stay one tap away, for a question that should not be remembered.
struct ThreadView: View {
    let thread: ThreadStore
    let memory: MemoryStore
    let chat: ChatStore

    @State private var typed = ""
    @State private var isMemoryPresented = false
    @State private var isContextCardPresented = false
    @FocusState private var isComposerFocused: Bool

    /// The bottom of the transcript, scrolled to when a turn is added there.
    private static let bottom = "thread-bottom"

    var body: some View {
        NavigationStack {
            Group {
                switch thread.transcript {
                case .loading:
                    LoadingTranscript()
                case .unavailable(let failure):
                    unavailable(failure)
                case .loaded(let held), .stale(let held, _):
                    if held.isEmpty, thread.exchange.tail == nil {
                        nothingSaidYet
                    } else {
                        transcript(held)
                    }
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            // The pair every chat screen needs: a model bubble is `secondarySystemGroupedBackground`,
            // white in light mode, and vanishes on a plain white page.
            .background(Color(.systemGroupedBackground))
            .navigationTitle("Lionheart")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar { toolbar }
            .navigationDestination(for: ChatRoute.self) { route in
                switch route {
                case .conversations: ChatView(store: chat)
                }
            }
            .safeAreaInset(edge: .bottom) {
                VStack(spacing: 0) {
                    if offersQuickPrompts {
                        QuickPrompts(onAsk: ask)
                    }
                    ChatComposer(
                        typed: $typed,
                        isFocused: $isComposerFocused,
                        acceptsTyping: acceptsTyping,
                        isAwaitingReply: thread.exchange.isAwaitingReply,
                        onSend: send)
                }
            }
            .sheet(isPresented: $isMemoryPresented) {
                MemorySheet(store: memory)
            }
            .sheet(isPresented: $isContextCardPresented) {
                ContextCardSheet(store: chat)
            }
            .alert(
                "Earlier sessions could not be read",
                isPresented: Binding(
                    get: { thread.earlier.refusal != nil },
                    set: { if !$0 { thread.dismissEarlierRefusal() } })
            ) {
                Button("OK") { thread.dismissEarlierRefusal() }
            } message: {
                if let refusal = thread.earlier.refusal { Text(refusal.message) }
            }
        }
        .task { await thread.refresh() }
        .task { await chat.refreshContextCard() }
        .onChange(of: thread.exchange.unsentText) { _, unsent in
            if let unsent, typed.isEmpty { typed = unsent }
        }
    }

    // MARK: - Transcript

    private func transcript(_ held: ChatThread) -> some View {
        let rows = ThreadTranscript.rows(held, tail: thread.exchange.tail)
        return ScrollViewReader { proxy in
            ScrollView {
                LazyVStack(alignment: .leading, spacing: 14) {
                    if held.hasEarlier { earlierButton }
                    if case .stale(_, let staleness) = thread.transcript {
                        StaleBanner(staleness)
                    }
                    ForEach(rows) { row in
                        switch row {
                        case .opened(_, let at):
                            SessionOpening(at: at)
                        case .turn(let turn):
                            TurnBubble(
                                turn: turn,
                                revealsOnArrival: turn.id == thread.arrivedReply,
                                onRevealed: { thread.settle(reply: turn.id) })
                        }
                    }
                    if thread.exchange.isAwaitingReply {
                        WorkingRow()
                    }
                    Color.clear.frame(height: 1).id(Self.bottom)
                }
                .padding(.horizontal, Tokens.cardPadding)
                .padding(.vertical, 16)
            }
            // Opens at the newest turn. Only the opening: reading earlier sessions grows the top,
            // and the athlete who asked for them should be left where he is.
            .defaultScrollAnchor(.bottom, for: .initialOffset)
            .scrollDismissesKeyboard(.interactively)
            .onChange(of: rows.last?.id) {
                withAnimation { proxy.scrollTo(Self.bottom, anchor: .bottom) }
            }
        }
    }

    private var earlierButton: some View {
        Button {
            Task { await thread.loadEarlier() }
        } label: {
            if thread.earlier.isRunning {
                ProgressView()
            } else {
                Text("Earlier sessions")
            }
        }
        .font(.footnote)
        .frame(maxWidth: .infinity)
        .disabled(thread.earlier.isRunning)
    }

    // MARK: - Empty and not loaded

    private var nothingSaidYet: some View {
        ContentUnavailableView {
            Label("One continuing thread", systemImage: "bubble.left.and.text.bubble.right")
        } description: {
            Text("Ask about your training here at any time. After three hours without a message, the session is distilled into memory and the next one starts from it.")
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
                Button("Try again") { Task { await thread.refresh() } }
            }
        }
    }

    // MARK: - Chrome

    @ToolbarContentBuilder
    private var toolbar: some ToolbarContent {
        ToolbarItem(placement: .topBarLeading) {
            NavigationLink(value: ChatRoute.conversations) {
                Label("Conversations", systemImage: "list.bullet")
            }
            .disabled(thread.exchange.isAwaitingReply)
        }
        ToolbarItemGroup(placement: .topBarTrailing) {
            Button("Memory", systemImage: "note.text") {
                isMemoryPresented = true
            }
            Button("Athlete context", systemImage: "person.text.rectangle") {
                isContextCardPresented = true
            }
        }
    }

    private var acceptsTyping: Bool {
        thread.transcript.value != nil && !thread.exchange.isAwaitingReply
    }

    /// Only while nothing is typed and the keyboard is down: a half-written question is the
    /// athlete's own, and the row would sit between him and it.
    private var offersQuickPrompts: Bool {
        acceptsTyping && typed.isEmpty && !isComposerFocused
    }

    private func send() {
        let words = typed
        typed = ""
        isComposerFocused = false
        Task { await thread.send(words) }
    }

    private func ask(_ question: String) {
        Task { await thread.send(question) }
    }
}

/// Where the Chat tab's stack can go from the thread.
enum ChatRoute: Hashable {
    /// The standalone conversations, opening on the one last added to.
    case conversations
}

/// The seam between two sessions: when the later one opened.
private struct SessionOpening: View {
    let at: Date

    var body: some View {
        let label = ThreadTranscript.openingLabel(at, now: Date())
        Text(label)
            .font(.caption)
            .foregroundStyle(Color(.secondaryLabel))
            .frame(maxWidth: .infinity)
            .padding(.top, 6)
            .accessibilityLabel("Session opened \(label)")
    }
}
