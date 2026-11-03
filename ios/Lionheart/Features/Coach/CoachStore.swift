import Foundation
import Observation

/// The Coach tab's data: the conversation on screen, the list to switch between, and the context
/// card the model is given.
///
/// **Every model call this store makes is serial, deliberately.** In development the chat provider
/// is a local LM Studio server that cannot serve concurrent chat requests — four at once returns
/// three failures — and the context card's rebuild makes a narrative call of its own. So the card
/// is read when the tab opens, refreshed only on request, and never while an exchange is out; and
/// an exchange cannot start while one already is. This is the one store in the app where `async
/// let` would be a defect rather than an improvement.
@Observable
@MainActor
final class CoachStore {
    private(set) var state: CoachConversationState = .loading
    private(set) var conversations: [ChatConversation] = []
    private(set) var contextCard: LoadState<AthleteContextCard> = .loading

    private let chat: ChatAPI
    private let profile: ProfileAPI

    private var openTask: Task<Void, Never>?
    private var replyTask: Task<Void, Never>?
    private var cardTask: Task<Void, Never>?

    init(chat: ChatAPI, profile: ProfileAPI) {
        self.chat = chat
        self.profile = profile
    }

    /// Opens the tab: the conversation list, then the one last added to.
    ///
    /// Sequential rather than parallel, and not because of the model this time but because of the
    /// ordering: the list is what says whether a conversation exists at all, which is what decides
    /// between "loading one" and "you have not asked anything yet".
    func refresh() async {
        guard !state.isAwaitingReply else { return }
        openTask?.cancel()
        let task = Task { await self.openMostRecent() }
        openTask = task
        await task.value
    }

    func reset() {
        openTask?.cancel()
        replyTask?.cancel()
        cardTask?.cancel()
        openTask = nil
        replyTask = nil
        cardTask = nil
        state = .loading
        conversations = []
        contextCard = .loading
    }

    /// Switches to a conversation the athlete picked.
    ///
    /// Refused while a reply is out: the answer that comes back belongs to the conversation the
    /// question went to, and moving the screen underneath it would file the answer under the wrong
    /// chat.
    func open(_ conversationId: UUID) async {
        guard !state.isAwaitingReply else { return }
        openTask?.cancel()
        let task = Task { await self.read(conversationId) }
        openTask = task
        await task.value
    }

    /// Clears the screen for a new conversation.
    ///
    /// The conversation itself is created by the first message rather than here, because a chat
    /// with no turns in it is a row in the list with nothing to show and no way to delete itself
    /// except by hand.
    func startNewConversation() {
        guard !state.isAwaitingReply else { return }
        openTask?.cancel()
        state = .none
    }

    /// Sends one turn and waits for the whole exchange.
    ///
    /// Three things this has to get right, each from the server's actual behaviour:
    ///
    /// - It creates the conversation first when there is none, because `POST /message/process`
    ///   needs one to post into.
    /// - The athlete's words go on screen before the request leaves. An exchange takes tens of
    ///   seconds, and a screen that swallowed the question for that long reads as broken.
    /// - A failure leaves the words `.unsent` rather than dropping them, because **the server did
    ///   not keep them**: it stages the message and saves only once the model has answered.
    func send(_ text: String) async {
        let words = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !words.isEmpty, !state.isAwaitingReply else { return }

        replyTask?.cancel()
        let task = Task { await self.runExchange(words) }
        replyTask = task
        await task.value
    }

    /// Reads the context card. Refused while a reply is out — the card's rebuild makes its own
    /// model call, and the development provider serves one at a time.
    func refreshContextCard() async {
        guard !state.isAwaitingReply else { return }
        cardTask?.cancel()
        let task = Task { await self.readCard() }
        cardTask = task
        await task.value
    }

    func deleteConversation(_ conversationId: UUID) async {
        guard !state.isAwaitingReply else { return }
        // A delete that fails leaves the conversation where it is, and the reload below shows that
        // truthfully. There is nothing to tell the athlete that the unchanged list does not.
        try? await chat.deleteConversation(id: conversationId)
        if state.conversation?.chatConversationId == conversationId {
            state = .none
        }
        await refresh()
    }

    // MARK: -

    private func openMostRecent() async {
        do {
            conversations = try await chat.conversations()
            guard let newest = newestConversationId else {
                state = .none
                return
            }
            state = state.applying(.success(try await chat.conversation(id: newest)))
        } catch is CancellationError {
            return
        } catch APIError.notFound {
            // No conversations. An empty state, not a failure: the athlete has not asked anything.
            state = .none
        } catch {
            state = state.applying(.failure(failure(from: error)))
        }
    }

    private func read(_ conversationId: UUID) async {
        do {
            state = state.applying(.success(try await chat.conversation(id: conversationId)))
        } catch is CancellationError {
            return
        } catch {
            state = state.applying(.failure(failure(from: error)))
        }
    }

    private func runExchange(_ words: String) async {
        let pending = CoachTurn.pending(id: UUID(), text: words)

        do {
            let conversation = try await conversationToPostInto(titledBy: words)
            state = .awaitingReply(conversation, pending: pending)

            _ = try await chat.send(words, to: conversation.chatConversationId)

            // The reply route answers with the model's final message alone, so the transcript is
            // re-read rather than appended to: one exchange can also store tool results, and a
            // read is the only way the screen reflects what was actually kept.
            state = state.applying(.success(try await chat.conversation(id: conversation.chatConversationId)))
            conversations = (try? await chat.conversations()) ?? conversations
        } catch is CancellationError {
            // The only thing that cancels an exchange is `reset`, which has already emptied the
            // tab for the next athlete. Writing a refusal here would put the last athlete's
            // question back on a screen that is meant to be blank.
            return
        } catch {
            handed(pending, back: failure(from: error))
        }
    }

    /// Puts the athlete's words back where they can resend them.
    private func handed(_ pending: CoachTurn, back failure: LoadFailure) {
        guard let conversation = state.conversation else {
            state = .unavailable(failure)
            return
        }
        state = .refused(conversation, unsent: .unsent(id: pending.id, text: pending.text, failure: failure))
    }

    /// The conversation to post into: the one open, or a new one named after the question.
    private func conversationToPostInto(titledBy words: String) async throws -> ChatConversation {
        if let open = state.conversation { return open }
        let created = try await chat.createConversation(named: Self.title(from: words))
        conversations.insert(created, at: 0)
        return created
    }

    private func readCard() async {
        do {
            contextCard = contextCard.applying(.success(try await profile.contextCard()))
        } catch is CancellationError {
            return
        } catch {
            contextCard = contextCard.applying(.failure(failure(from: error)))
        }
    }

    private var newestConversationId: UUID? {
        conversations
            .max { ($0.lastUpdate ?? .distantPast) < ($1.lastUpdate ?? .distantPast) }?
            .chatConversationId
    }

    private func failure(from error: any Error) -> LoadFailure {
        (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))
    }

    /// A conversation's name, taken from its first question.
    ///
    /// The server does no auto-naming, and an unnamed chat is unfindable in a list a month later.
    /// Clipped on a word boundary so a title does not end mid-word.
    ///
    /// `nonisolated` because it touches no actor state: it is a rule about a string, and stating
    /// that is what lets it be unit-tested without a main-actor hop.
    nonisolated static func title(from words: String) -> String {
        let collapsed = words.split(whereSeparator: \.isWhitespace).joined(separator: " ")
        guard collapsed.count > Self.titleLength else { return collapsed }
        let clipped = collapsed.prefix(Self.titleLength)
        let onWord = clipped.lastIndex(of: " ").map { String(clipped[..<$0]) } ?? String(clipped)
        return "\(onWord)…"
    }

    /// Long enough to tell two questions apart in a list, short enough for one line.
    nonisolated private static let titleLength = 48
}
