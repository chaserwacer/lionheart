import SwiftUI

/// The field and the send button, floating on glass over a transcript — shared by the thread and
/// the standalone conversations.
///
/// Sibling glass surfaces in one container rather than an opaque bar: it is the composer every
/// messaging app on this platform now has, and the transcript scrolling visibly under it is what
/// says the conversation continues below. The container is what makes them one piece of glass —
/// separate ones this close together bend light against each other.
///
/// On iOS 26 the row starts with the tab bar, minimised to the Chat tab's glyph (`InlineComposer`),
/// so the bottom of the screen is one row: the tabs, folded, then the field.
struct ChatComposer: View {
    @Binding var typed: String
    var isFocused: FocusState<Bool>.Binding
    let acceptsTyping: Bool
    /// A line above the field saying what the wait is, for a screen whose answer arrives whole.
    var waitingNote: String? = nil
    /// Unfolds the tab bar, when the row starts with it folded.
    var showTabs: (() -> Void)? = nil
    let onSend: () -> Void

    /// Chat has nothing to answer from memory: every answer is the model reading the server. So
    /// when the server is away the composer says so before the athlete types.
    @Environment(ServerReachability.self) private var reachability

    /// A glyph in a circle has to be given a size, and a given size does not grow with Dynamic Type
    /// on its own — so it is scaled, from the 44pt minimum touch target at the default text size.
    @ScaledMetric private var sendDiameter: CGFloat = 44

    var body: some View {
        VStack(spacing: 8) {
            if !reachability.isReachable, acceptsTyping {
                caption("Server unreachable. Chat needs a connection to it.")
            }
            if let waitingNote {
                caption(waitingNote)
            }

            GlassChromeGroup(spacing: 10) {
                HStack(alignment: .bottom, spacing: 10) {
                    if let showTabs {
                        FoldedTabBar {
                            isFocused.wrappedValue = false
                            showTabs()
                        }
                    }

                    TextField("Ask a question", text: $typed, axis: .vertical)
                        .lineLimit(1...5)
                        .textFieldStyle(.plain)
                        .padding(.horizontal, 16)
                        .padding(.vertical, 10)
                        .glassChrome(in: .capsule)
                        .focused(isFocused)
                        .disabled(!acceptsTyping)

                    Button(action: send) {
                        Image(systemName: "arrow.up")
                            .font(.body.weight(.semibold))
                            .foregroundStyle(canSend ? Tokens.accent : Color(.tertiaryLabel))
                            .frame(width: sendDiameter, height: sendDiameter)
                            .glassChrome(in: .circle, interactive: true)
                    }
                    .buttonStyle(.plain)
                    .disabled(!canSend)
                    .accessibilityLabel("Send")
                }
            }
        }
        .padding(.horizontal, Tokens.cardPadding)
        .padding(.vertical, 10)
    }

    private var canSend: Bool {
        reachability.isReachable && acceptsTyping && !typed.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    private func send() {
        guard canSend else { return }
        onSend()
    }

    private func caption(_ text: String) -> some View {
        Text(text)
            .font(.caption)
            .foregroundStyle(Color(.secondaryLabel))
            .frame(maxWidth: .infinity, alignment: .leading)
    }
}

/// One turn. The athlete's words sit right and tinted, the model's left and plain — the arrangement
/// every messaging app on this platform uses, so it needs no explaining.
struct TurnBubble: View {
    let turn: ChatTurn

    /// Whether this is the answer that has just arrived, set down word by word rather than all at
    /// once (`ChatReveal`). Only ever the newest answer, and never with Reduce Motion on.
    var revealsOnArrival = false

    /// Told once the answer is fully set down, so the screen stops treating it as arriving.
    var onRevealed: () -> Void = {}

    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var arrivedAt = Date()
    @State private var isRevealed = false

    var body: some View {
        HStack {
            if turn.isFromAthlete { Spacer(minLength: 40) }

            VStack(alignment: turn.isFromAthlete ? .trailing : .leading, spacing: 4) {
                words
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
                    // Said plainly because it is unusual: the server keeps the question only once
                    // the model has answered, so a failed exchange leaves no record of it at all.
                    Label("Not sent — \(failure.message)", systemImage: "exclamationmark.circle")
                        .font(.caption)
                        .foregroundStyle(.red)
                }
            }

            if !turn.isFromAthlete { Spacer(minLength: 40) }
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel(turn.isFromAthlete ? "You said" : "Tempo said")
        .accessibilityValue(turn.text)
        .task(id: turn.id) {
            guard isRevealing else { return }
            guard (try? await Task.sleep(for: .seconds(ChatReveal.duration(of: String(prose.characters))))) != nil
            else { return }
            isRevealed = true
            onRevealed()
        }
    }

    private var prose: AttributedString {
        turn.isFromAthlete ? AttributedString(turn.text) : ChatProse.attributed(turn.text)
    }

    private var isRevealing: Bool {
        revealsOnArrival && !reduceMotion && !turn.isFromAthlete && !isRevealed
    }

    /// The words, or as many of them as have been set down. The whole answer is laid out from the
    /// first frame, invisibly, so the bubble has its final size at once and the transcript does not
    /// shift under the athlete as the words fill it.
    @ViewBuilder
    private var words: some View {
        let full = prose
        if isRevealing {
            Text(full)
                .hidden()
                .overlay(alignment: .topLeading) {
                    TimelineView(.animation(minimumInterval: 1.0 / 30)) { context in
                        Text(Self.prefix(of: full, after: context.date.timeIntervalSince(arrivedAt)))
                    }
                }
        } else {
            Text(full)
        }
    }

    private static func prefix(of full: AttributedString, after elapsed: TimeInterval) -> AttributedString {
        let shown = ChatReveal.visibleCount(of: String(full.characters), after: elapsed)
        let end = full.characters.index(full.startIndex, offsetBy: shown)
        return AttributedString(full[full.startIndex..<end])
    }

    private var background: Color {
        switch turn {
        case .pending: Tokens.accent.opacity(0.6)
        case .unsent: Color(.systemFill)
        case .said: turn.isFromAthlete ? Tokens.accent : Color(.secondarySystemGroupedBackground)
        }
    }
}

/// A bubble with nothing in it yet, for the moment before a transcript has been read.
///
/// Its own view rather than a `TurnBubble` holding invented turns: a screen should not have to
/// fabricate domain values to draw its own waiting state.
struct PlaceholderBubble: View {
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

/// The transcript's own shape while it is being read: two bubbles, the athlete's and the model's,
/// in grey.
struct LoadingTranscript: View {
    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            PlaceholderBubble(text: "What did my last week of training look like?", isFromAthlete: true)
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
}

/// The waiting row, which is the whole reason a chat screen does not look hung.
///
/// It counts, because a spinner with no number is indistinguishable from a spinner that has stopped
/// meaning anything. The spinner is the mark building, since it is Tempo that is working. `TimelineView(.periodic)` ticks the label and nothing else — the transcript
/// above it does not redraw once a second. The count runs from when the row appeared, through every
/// change of what it says, so it is always the length of the whole wait.
struct WorkingRow: View {
    /// What the wait is for.
    enum Activity: Equatable {
        /// Nothing is known beyond the time, which is all a screen whose answer arrives whole has.
        case answering
        /// The model is reading these tools.
        case consulting(tools: [String])
        /// The connection went; the answer is being looked for in the thread.
        case recovering
    }

    var activity: Activity = .answering

    /// When the wait started. `@State` initialised at its declaration, so it is the moment this
    /// row appeared rather than the moment the view was recreated.
    @State private var startedAt = Date()

    var body: some View {
        HStack(spacing: 10) {
            MarkLoadingIndicator(height: 14)
            TimelineView(.periodic(from: startedAt, by: 1)) { context in
                Text(waiting(for: context.date.timeIntervalSince(startedAt)))
                    .font(.footnote.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
        .padding(.horizontal, 14)
        .padding(.vertical, 10)
        .background(Color(.secondarySystemGroupedBackground), in: .rect(cornerRadius: 18))
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(spoken)
    }

    /// Says what it is doing, not just that it is busy. With nothing else to go on, it mentions the
    /// tool loop only once the wait is long enough for the athlete to be wondering.
    private func waiting(for elapsed: TimeInterval) -> String {
        let seconds = Int(max(0, elapsed))
        switch activity {
        case .answering:
            guard seconds >= 8 else { return "Thinking… \(seconds)s" }
            return "Reading your sessions, wellness and Oura days… \(seconds)s"
        case .consulting(let tools):
            return "Reading \(ReplyProgress.reading(tools))… \(seconds)s"
        case .recovering:
            return "Connection lost. Finding the answer in the thread… \(seconds)s"
        }
    }

    private var spoken: String {
        switch activity {
        case .answering: "Tempo is working on an answer"
        case .consulting(let tools): "Tempo is reading \(ReplyProgress.reading(tools))"
        case .recovering: "Connection lost. Tempo is finding the answer in the thread"
        }
    }
}

/// The answer while it is being written: the model's bubble, filling as the words arrive.
///
/// Silent to VoiceOver until it is whole. Read out as it grows, an answer would be interrupted by
/// its own next words every moment; the screen says it once, when it has been stored.
struct WritingBubble: View {
    let text: String

    var body: some View {
        HStack {
            Text(ChatProse.attributed(text))
                .font(.body)
                .foregroundStyle(Color(.label))
                .padding(.horizontal, 14)
                .padding(.vertical, 10)
                .background(Color(.secondarySystemGroupedBackground), in: .rect(cornerRadius: 18))
            Spacer(minLength: 40)
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Tempo is answering")
    }
}
