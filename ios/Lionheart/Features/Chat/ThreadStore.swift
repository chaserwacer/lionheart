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

    /// An answer this screen has just received whole rather than watched being written — read back
    /// from the thread after the connection went — which is set down word by word as it lands. An
    /// answer the athlete watched arrive, or any read back on a refresh, is shown as it stands.
    private(set) var arrivedReply: UUID?

    /// The answer most recently completed here, for the screen to say once to VoiceOver. Nothing
    /// is said while an answer is being written: the athlete hears it when it is whole.
    private(set) var answered: ChatMessage?

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
        arrivedReply = nil
        answered = nil
    }

    /// The arrived answer has been set down, so it is shown whole from now on, even when its bubble
    /// is rebuilt by scrolling it away and back.
    func settle(reply id: UUID) {
        if arrivedReply == id { arrivedReply = nil }
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

    /// Sends one message and follows the exchange to its end.
    ///
    /// The words go on screen before the request leaves, and a refusal hands them back to the
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

    /// Sends the words, then follows the answer as it is written.
    ///
    /// Cancellation ends it silently at every step: only `reset` cancels an exchange, and it has
    /// already emptied the tab for the next athlete, so putting the question back would show it on
    /// their screen.
    private func runExchange(_ words: String) async {
        let pending = ChatTurn.pending(id: UUID(), text: words)
        let askedAt = Date()
        exchange = .awaitingReply(pending, .thinking)

        let replies: ThreadReplies
        do {
            replies = try await api.ask(words)
        } catch is CancellationError {
            return
        } catch {
            exchange = .refused(.unsent(id: pending.id, text: words, failure: failure(from: error)))
            return
        }

        do {
            for try await event in replies {
                switch event {
                case .wrote, .consulting:
                    if case .awaitingReply(_, let progress) = exchange {
                        exchange = .awaitingReply(pending, progress.after(event))
                    }
                case .stored(let session):
                    arrived(session, watched: true)
                    return
                case .refused(let refusal):
                    exchange = .refused(.unsent(id: pending.id, text: words, failure: failure(from: refusal)))
                    return
                case .unrecognised:
                    continue
                }
            }
        } catch {
            // A cancelled task reads its bytes as a cancelled connection, which is not a lost one.
            if Task.isCancelled { return }
        }

        await recover(pending, askedAt: askedAt)
    }

    /// The answer arrived, as the session it was stored in.
    ///
    /// - Parameter watched: Whether the athlete watched it being written. One that arrives whole —
    ///   read back from the thread after the connection went — is set down as it lands instead
    ///   (`ChatReveal`), so it still reads as arriving rather than appearing.
    private func arrived(_ session: ThreadSession, watched: Bool) {
        let answer = session.transcript.last { !ChatTurn.said($0).isFromAthlete }
        arrivedReply = watched ? nil : answer?.chatMessageId
        transcript = transcript.editing { $0.with(session) }
        exchange = .idle
        if let answer { answered = answer }
    }

    /// The connection went while the answer was arriving. The server finishes and stores it
    /// regardless, so the thread is read until it holds the answer, for as long as an answer could
    /// take. Only then are the words handed back, saying what is known: not that nothing was kept.
    private func recover(_ pending: ChatTurn, askedAt: Date) async {
        exchange = .recovering(pending)
        let deadline = askedAt.addingTimeInterval(HTTP.modelPatience)

        while Date() < deadline {
            guard (try? await Task.sleep(for: Self.recoveryInterval)) != nil else { return }
            // Only the server's own answer counts: a thread recalled from the device predates the
            // question, and failing to reach the server here is the reason to keep looking.
            guard case .current(let thread)? = try? await api.latest(remembering: .chatThread),
                  ThreadTranscript.answer(to: pending.text, askedAt: askedAt, in: thread) != nil,
                  let session = thread.sessions.last
            else { continue }
            transcript = transcript.applying(.current(thread))
            arrived(session, watched: false)
            return
        }
        exchange = .refused(.unsent(
            id: pending.id, text: pending.text,
            failure: .offline("The connection dropped before the answer arrived, and it has not reached the thread yet. Sending again asks twice.")))
    }

    private static let recoveryInterval: Duration = .seconds(3)

    private func failure(from error: any Error) -> LoadFailure {
        (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))
    }
}
