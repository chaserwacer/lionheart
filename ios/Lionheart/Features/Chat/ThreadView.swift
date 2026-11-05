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
    /// Whether the athlete has unfolded the tab bar, which puts the composer away (`InlineComposer`).
    @State private var showsTabs = false
    /// Whether the transcript is at its bottom, where a growing answer carries it down.
    @State private var isFollowing = true
    @FocusState private var isComposerFocused: Bool
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    /// The bottom of the transcript, scrolled to when a turn is added there.
    private static let bottom = "thread-bottom"

    /// How near the bottom the athlete has to be reading for a growing answer to carry him down.
    private static let followDistance: CGFloat = 80

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
            .composerInlineWithTabBar(showsTabs: $showsTabs)
            // The pair every chat screen needs: a model bubble is `secondarySystemGroupedBackground`,
            // white in light mode, and vanishes on a plain white page.
            .background(Color(.systemGroupedBackground))
            .navigationTitle("Tempo")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar { toolbar }
            .navigationDestination(for: ChatRoute.self) { route in
                switch route {
                case .conversations: ChatView(store: chat)
                }
            }
            .safeAreaInset(edge: .bottom) {
                if !showsTabs {
                    ChatComposer(
                        typed: $typed,
                        isFocused: $isComposerFocused,
                        acceptsTyping: acceptsTyping,
                        showTabs: unfoldsTabs,
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
        // Said once, whole, when it is stored: the bubble it was written into is silent meanwhile.
        .onChange(of: thread.answered?.id) {
            guard let answer = thread.answered else { return }
            let words = String(ChatProse.attributed(answer.text).characters)
            AccessibilityNotification.Announcement("Tempo answered. \(words)").post()
        }
    }

    // MARK: - Transcript

    private func transcript(_ held: ChatThread) -> some View {
        let rows = ThreadTranscript.rows(held, tail: thread.exchange.tail)
        return ScrollViewReader { proxy in
            ScrollView {
                // Not lazy. A lazy stack opened at its bottom estimates the height of the rows it
                // has not drawn, and on iOS 26 it opened past the end of the transcript, blank,
                // when those rows were long answers. A page is ten sessions, so drawing every row
                // costs little.
                VStack(alignment: .leading, spacing: 14) {
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
                    arriving
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
                withAnimation(reduceMotion ? nil : .default) { proxy.scrollTo(Self.bottom, anchor: .bottom) }
            }
            // An answer being written grows the bottom, and the transcript follows it down while
            // the athlete is reading there; scrolled up to read something else, he is left there.
            // Not animated: a scroll animated at the pace words arrive reads as the screen shaking.
            .onScrollGeometryChange(for: Bool.self) { geometry in
                geometry.contentOffset.y + geometry.containerSize.height
                    >= geometry.contentSize.height - Self.followDistance
            } action: { _, isAtBottom in
                isFollowing = isAtBottom
            }
            .onChange(of: writtenSoFar?.count) {
                if isFollowing { proxy.scrollTo(Self.bottom, anchor: .bottom) }
            }
        }
    }

    /// The answer on its way: what it is doing until it starts writing, then the words so far.
    ///
    /// One waiting row whatever it says, so its count runs on from thinking into reading tools.
    @ViewBuilder
    private var arriving: some View {
        if let written = writtenSoFar {
            WritingBubble(text: written)
        } else if let activity = waitingFor {
            WorkingRow(activity: activity)
        }
    }

    private var writtenSoFar: String? {
        switch thread.exchange {
        case .awaitingReply(_, let progress): progress.written
        case .idle, .recovering, .refused: nil
        }
    }

    private var waitingFor: WorkingRow.Activity? {
        switch thread.exchange {
        case .awaitingReply(_, .thinking): .answering
        case .awaitingReply(_, .consulting(let tools)): .consulting(tools: tools)
        case .recovering: .recovering
        case .awaitingReply(_, .writing), .idle, .refused: nil
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
            Label {
                Text("One continuing thread")
            } icon: {
                BrandMark().frame(height: 40)
            }
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
        // The model's name with its mark, since this is the one screen where the app speaks.
        ToolbarItem(placement: .principal) {
            HStack(spacing: 6) {
                BrandMark().frame(height: 14)
                Text("Tempo").font(.headline)
            }
            // The bar offers the principal slot no more than its own measure, which truncated the name.
            .fixedSize()
            .accessibilityElement(children: .combine)
            .accessibilityAddTraits(.isHeader)
        }
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

    private func send() {
        let words = typed
        typed = ""
        isComposerFocused = false
        Task { await thread.send(words) }
    }

    /// Unfolds the tab bar from the composer's row, where the OS draws the row with one.
    private var unfoldsTabs: (() -> Void)? {
        guard InlineComposer.isAvailable else { return nil }
        return { withAnimation(.snappy) { showsTabs = true } }
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
            .font(.meta)
            .foregroundStyle(Color(.secondaryLabel))
            .frame(maxWidth: .infinity)
            .padding(.top, 6)
            .accessibilityLabel("Session opened \(label)")
    }
}
