import Foundation
import Observation

/// The Chat tab's default screen: the athlete's one continuing thread with Lionheart.
///
/// The transcript and the exchange are two states rather than one because they vary apart: the
/// thread can be recalled offline and still hold a question that failed to send, and a reply can be
/// awaited over a transcript that is not current. A question only leaves once a transcript is on
/// screen, so an exchange is never out over nothing.
///
/// Like `ChatStore`, its model calls are serial: one exchange at a time, and no read of the thread
/// while one is out, since the answer replaces the session it went into.
@Observable
@MainActor
final class ThreadStore {
    private(set) var transcript: LoadState<ChatThread> = .loading
    private(set) var exchange: ThreadExchange = .idle

    /// Where a read of earlier sessions stands.
    private(set) var earlier: ActionState = .idle

    private let api: ChatThreadAPI

    private var load: Task<Void, Never>?
    private var reply: Task<Void, Never>?

    init(api: ChatThreadAPI) {
        self.api = api
    }

    func refresh() async {
        guard !exchange.isAwaitingReply else { return }
        load?.cancel()
        let task = Task { await self.reload() }
        load = task
        await task.value
    }

    func reset() {
        load?.cancel()
        reply?.cancel()
        load = nil
        reply = nil
        transcript = .loading
        exchange = .idle
        earlier = .idle
    }

    /// Reads the stretch before the oldest session on screen and puts it in front.
    func loadEarlier() async {
        guard !earlier.isRunning, let thread = transcript.value, thread.hasEarlier,
              let oldest = thread.oldestOpening
        else { return }

        earlier = .running
        do {
            let page = try await api.sessions(openedBefore: oldest)
            transcript = transcript.editing { $0.prepending(page) }
            earlier = .idle
        } catch {
            earlier = ActionState.refusal(from: error)
        }
    }

    func dismissEarlierRefusal() {
        earlier = .idle
    }

    /// Sends one message and waits for the whole exchange.
    ///
    /// The words go on screen before the request leaves, and a failure hands them back to the
    /// composer rather than dropping them, because the server kept nothing.
    func send(_ text: String) async {
        let words = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !words.isEmpty, !exchange.isAwaitingReply, transcript.value != nil else { return }

        reply?.cancel()
        let task = Task { await self.runExchange(words) }
        reply = task
        await task.value
    }

    // MARK: -

    private func reload() async {
        do {
            transcript = transcript.applying(try await api.latest(remembering: .chatThread))
        } catch is CancellationError {
            return
        } catch {
            transcript = transcript.applying(.failure(failure(from: error)))
        }
    }

    private func runExchange(_ words: String) async {
        let pending = ChatTurn.pending(id: UUID(), text: words)
        exchange = .awaitingReply(pending)

        do {
            let session = try await api.send(words)
            transcript = transcript.editing { $0.with(session) }
            exchange = .idle
        } catch is CancellationError {
            // Only `reset` cancels an exchange, and it has already emptied the tab for the next
            // athlete; putting the question back would show it on their screen.
            return
        } catch {
            exchange = .refused(.unsent(id: pending.id, text: words, failure: failure(from: error)))
        }
    }

    private func failure(from error: any Error) -> LoadFailure {
        (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))
    }
}
