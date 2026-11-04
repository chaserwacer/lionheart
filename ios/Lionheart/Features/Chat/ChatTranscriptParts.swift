import SwiftUI

/// The field and the send button, floating on glass over a transcript — shared by the thread and
/// the standalone conversations.
///
/// Two sibling glass surfaces in one container rather than an opaque bar: it is the composer every
/// messaging app on this platform now has, and the transcript scrolling visibly under it is what
/// says the conversation continues below. The container is what makes them one piece of glass —
/// two separate ones this close together bend light against each other.
struct ChatComposer: View {
    @Binding var typed: String
    var isFocused: FocusState<Bool>.Binding
    let acceptsTyping: Bool
    let isAwaitingReply: Bool
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
            if isAwaitingReply {
                caption("Reading your training data. This can take a minute.")
            }

            GlassChromeGroup(spacing: 10) {
                HStack(alignment: .bottom, spacing: 10) {
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
        .accessibilityLabel(turn.isFromAthlete ? "You said" : "Lionheart said")
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

/// The questions asked most days, one tap from being asked.
///
/// Capsules on the same glass as the composer, in a row that scrolls sideways, so the row stays
/// one line at any text size and a question never wraps into two. Glass rather than a bordered
/// button's tint because the transcript scrolls under the composer, and a tint alone lets the words
/// beneath it read through the question. Each is sent as written, exactly as if typed: the briefing
/// that goes with every message is what makes a one-line question enough.
struct QuickPrompts: View {
    let onAsk: (String) -> Void

    @Environment(ServerReachability.self) private var reachability

    static let questions = [
        "Should I train today?",
        "Am I recovered?",
        "How was my week?",
        "What should I do for cardio?",
        "Plan my next 7 days",
    ]

    var body: some View {
        ScrollView(.horizontal) {
            GlassChromeGroup(spacing: 8) {
                HStack(spacing: 8) {
                    ForEach(Self.questions, id: \.self) { question in
                        Button { onAsk(question) } label: {
                            Text(question)
                                .font(.subheadline)
                                .foregroundStyle(reachability.isReachable ? Tokens.accent : Color(.tertiaryLabel))
                                .padding(.horizontal, 14)
                                .padding(.vertical, 8)
                                .glassChrome(in: .capsule, interactive: true)
                        }
                        .buttonStyle(.plain)
                        .disabled(!reachability.isReachable)
                    }
                }
                .padding(.horizontal, Tokens.cardPadding)
                .padding(.vertical, 4)
            }
        }
        .scrollIndicators(.hidden)
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Suggested questions")
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
/// meaning anything. `TimelineView(.periodic)` ticks the label and nothing else — the transcript
/// above it does not redraw once a second.
struct WorkingRow: View {
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
        .accessibilityLabel("Lionheart is working on an answer")
    }

    /// Says what it is doing, not just that it is busy — and only mentions the tool loop once the
    /// wait is long enough for the athlete to be wondering.
    private func waiting(for elapsed: TimeInterval) -> String {
        let seconds = Int(max(0, elapsed))
        guard seconds >= 8 else { return "Thinking… \(seconds)s" }
        return "Reading your sessions, wellness and Oura days… \(seconds)s"
    }
}
