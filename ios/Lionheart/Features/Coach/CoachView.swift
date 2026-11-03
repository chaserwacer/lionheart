import SwiftUI

/// What does it mean?
///
/// A conversation the athlete scrolls and continues, over a composer. Not a `List`: a transcript is
/// bubbles of varying width, bottom-anchored, and `List`'s row insets and separators fight all
/// three — this is the one screen in the app where the inset-grouped default is the wrong answer.
///
/// The thing this screen has to get right is the **wait**. The model answers over the athlete's own
/// data through a tool loop and there is no streaming route, so a reply takes tens of seconds. The
/// question stays on screen, a working row sits under it counting, and the composer says what is
/// happening — anything less reads as hung.
struct CoachView: View {
    let store: CoachStore

    @State private var typed = ""
    @State private var isContextCardPresented = false
    @State private var isConversationListPresented = false
    @FocusState private var isComposerFocused: Bool

    var body: some View {
        NavigationStack {
            Group {
                switch store.state {
                case .loading:
                    loadingTranscript
                case .unavailable(let failure):
                    unavailable(failure)
                case .none:
                    nothingAskedYet
                case .reading, .awaitingReply, .refused:
                    transcript
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            // The grouped background every other tab gets from `List`. Without it the page is
            // plain white, and a coach bubble — `secondarySystemGroupedBackground`, which *is*
            // white in light mode — disappears into it. The two colours are a pair; using one
            // without the other is what makes the bubble invisible.
            .background(Color(.systemGroupedBackground))
            .navigationTitle(store.state.conversation?.title ?? "Coach")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar { toolbar }
            .safeAreaInset(edge: .bottom) { composer }
            .sheet(isPresented: $isContextCardPresented) {
                ContextCardSheet(store: store)
            }
            .sheet(isPresented: $isConversationListPresented) {
                ConversationListSheet(store: store)
            }
        }
        .task { await store.refresh() }
        .task { await store.refreshContextCard() }
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

    /// The transcript's own shape while the last conversation is being read: two bubbles, the
    /// athlete's and the coach's, in grey. The tab opens on a conversation that already exists far
    /// more often than not, so this is what is almost always coming.
    private var loadingTranscript: some View {
        VStack(alignment: .leading, spacing: 14) {
            PlaceholderBubble(
                text: "What did my last week of training look like?", isFromAthlete: true)
            PlaceholderBubble(
                text: "Four sessions, and the squat volume is up a little on the week before it.",
                isFromAthlete: false)
        }
        .padding(.horizontal, Tokens.cardPadding)
        .padding(.vertical, 16)
        .frame(maxHeight: .infinity, alignment: .top)
        .redacted(reason: .placeholder)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Loading")
    }

    private var nothingAskedYet: some View {
        ContentUnavailableView {
            Label("Ask the coach", systemImage: "bubble.left.and.text.bubble.right")
        } description: {
            Text("It answers over your own sessions, wellness and Oura days. Try asking what your last week looked like, or whether you are recovered enough to train.")
        } actions: {
            Button("What the coach knows about me") { isContextCardPresented = true }
        }
    }

    private func unavailable(_ failure: LoadFailure) -> some View {
        ContentUnavailableView {
            Label("The coach is not available", systemImage: "exclamationmark.triangle")
        } description: {
            Text(failure.message)
        } actions: {
            if failure.isWorthRetrying {
                Button("Try again") { Task { await store.refresh() } }
            }
        }
    }

    // MARK: - Chrome

    @ToolbarContentBuilder
    private var toolbar: some ToolbarContent {
        ToolbarItem(placement: .topBarLeading) {
            Button("Conversations", systemImage: "list.bullet") {
                isConversationListPresented = true
            }
            .disabled(store.state.isAwaitingReply)
        }
        ToolbarItemGroup(placement: .topBarTrailing) {
            Button("What the coach knows", systemImage: "person.text.rectangle") {
                isContextCardPresented = true
            }
            Button("New conversation", systemImage: "square.and.pencil") {
                store.startNewConversation()
                typed = ""
                isComposerFocused = true
            }
            .disabled(store.state.isAwaitingReply)
        }
    }

    private var composer: some View {
        VStack(spacing: 8) {
            if store.state.isAwaitingReply {
                Text("Reading your training data. This can take a minute.")
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
                    .frame(maxWidth: .infinity, alignment: .leading)
            }

            HStack(alignment: .bottom, spacing: 10) {
                TextField("Ask the coach", text: $typed, axis: .vertical)
                    .lineLimit(1...5)
                    .textFieldStyle(.plain)
                    .padding(.horizontal, 12)
                    .padding(.vertical, 8)
                    .background(Color(.tertiarySystemFill), in: .rect(cornerRadius: 18))
                    .focused($isComposerFocused)
                    .disabled(!store.state.acceptsTyping)

                Button {
                    send()
                } label: {
                    Image(systemName: "arrow.up.circle.fill")
                        .font(.title)
                        .symbolRenderingMode(.hierarchical)
                }
                .disabled(!canSend)
                .accessibilityLabel("Send")
            }
        }
        .padding(.horizontal, Tokens.cardPadding)
        .padding(.vertical, 10)
        .background(.bar)
    }

    private var canSend: Bool {
        store.state.acceptsTyping
            && !typed.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    private func send() {
        guard canSend else { return }
        let words = typed
        typed = ""
        isComposerFocused = false
        Task { await store.send(words) }
    }
}

/// One turn. The athlete's words sit right and tinted, the coach's left and plain — the arrangement
/// every messaging app on this platform uses, so it needs no explaining.
private struct TurnBubble: View {
    let turn: CoachTurn

    var body: some View {
        HStack {
            if turn.isFromAthlete { Spacer(minLength: 40) }

            VStack(alignment: turn.isFromAthlete ? .trailing : .leading, spacing: 4) {
                Text(turn.isFromAthlete ? AttributedString(turn.text) : CoachProse.attributed(turn.text))
                    .font(.body)
                    .foregroundStyle(turn.isFromAthlete ? Color.white : Color(.label))
                    .padding(.horizontal, 14)
                    .padding(.vertical, 10)
                    .background(background, in: .rect(cornerRadius: 18))
                    .textSelection(.enabled)
                    // A long press on the bubble itself, which is how every messaging app on the
                    // platform hands you a message. `textSelection` already allows a careful
                    // drag-and-copy; an answer the athlete wants to keep should not need one.
                    .contextMenu {
                        Button {
                            Clipboard.copy(turn.text)
                        } label: {
                            Label("Copy", systemImage: "doc.on.doc")
                        }
                        ShareLink(item: turn.text) {
                            Label("Share", systemImage: "square.and.arrow.up")
                        }
                    }

                if case .unsent(_, _, let failure) = turn {
                    // Said plainly because it is unusual: the server stages the question and saves
                    // it only once the model has answered, so a failed exchange leaves no record
                    // of it at all.
                    Label("Not sent — \(failure.message)", systemImage: "exclamationmark.circle")
                        .font(.caption)
                        .foregroundStyle(.red)
                }
            }

            if !turn.isFromAthlete { Spacer(minLength: 40) }
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel(turn.isFromAthlete ? "You said" : "The coach said")
        .accessibilityValue(turn.text)
    }

    private var background: Color {
        switch turn {
        case .pending: Tokens.accent.opacity(0.6)
        case .unsent: Color(.systemFill)
        case .said: turn.isFromAthlete ? Tokens.accent : Color(.secondarySystemGroupedBackground)
        }
    }
}

/// A bubble with nothing in it yet, for the moment before a conversation has been read.
///
/// Its own view rather than a `TurnBubble` holding invented turns: a screen should not have to
/// fabricate domain values to draw its own waiting state, and a fake `ChatMessage` with a minted id
/// is exactly the kind of thing that later gets mistaken for one.
private struct PlaceholderBubble: View {
    /// Never read — only measured. Its length is the bar's width.
    let text: String
    let isFromAthlete: Bool

    var body: some View {
        HStack {
            if isFromAthlete { Spacer(minLength: 40) }
            Text(text)
                .font(.body)
                .padding(.horizontal, 14)
                .padding(.vertical, 10)
                .background(
                    isFromAthlete ? Tokens.accent : Color(.secondarySystemGroupedBackground),
                    in: .rect(cornerRadius: 18))
            if !isFromAthlete { Spacer(minLength: 40) }
        }
    }
}

/// The waiting row, which is the whole reason this screen does not look hung.
///
/// It counts, because a spinner with no number is indistinguishable from a spinner that has stopped
/// meaning anything. `TimelineView(.periodic)` ticks the label and nothing else — the transcript
/// above it does not redraw once a second.
private struct WorkingRow: View {
    /// When the wait started. `@State` initialised at its declaration, so it is the moment this
    /// row appeared rather than the moment the view was recreated.
    @State private var startedAt = Date()

    var body: some View {
        HStack(spacing: 10) {
            ProgressView()
            TimelineView(.periodic(from: startedAt, by: 1)) { context in
                Text(waiting(for: context.date.timeIntervalSince(startedAt)))
                    .font(.footnote.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 10)
        .background(Color(.secondarySystemGroupedBackground), in: .rect(cornerRadius: 18))
        .accessibilityLabel("The coach is working on an answer")
    }

    /// Says what it is doing, not just that it is busy — and only mentions the tool loop once the
    /// wait is long enough for the athlete to be wondering.
    private func waiting(for elapsed: TimeInterval) -> String {
        let seconds = Int(max(0, elapsed))
        guard seconds >= 8 else { return "Thinking… \(seconds)s" }
        return "Reading your sessions, wellness and Oura days… \(seconds)s"
    }
}
