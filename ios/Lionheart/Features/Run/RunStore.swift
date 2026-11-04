import Foundation
import Observation

/// The runs imported from Apple Health, each read whole for its page and kept per session.
///
/// App-wide because a run's page is reached from three stacks — a session's detail in History or
/// Today, and the cardio screen on Body — and each should open onto the run already read. Each read
/// is remembered, so a run read once can be opened again with no signal.
@Observable
@MainActor
final class RunStore {
    private(set) var runs: [UUID: LoadState<RecordedRun>] = [:]

    private let health: HealthAPI

    init(health: HealthAPI) {
        self.health = health
    }

    /// The run behind a session. A failed re-read keeps the run already shown.
    func load(_ trainingSessionId: UUID) async {
        do {
            let read = try await health.run(trainingSessionId, remembering: .recordedRun(trainingSessionId))
            runs[trainingSessionId] = (runs[trainingSessionId] ?? .loading).applying(read)
        } catch is CancellationError {
            return
        } catch {
            runs[trainingSessionId] = (runs[trainingSessionId] ?? .loading).applying(.failure(
                (try? LoadFailure.from(error)) ?? .unavailable(String(describing: error))))
        }
    }

    /// Reads again every run already read — after a correction, or once the server answers again.
    func refreshLoaded() async {
        for id in runs.keys { await load(id) }
    }

    var isWaitingOnServer: Bool { runs.values.contains(where: \.isWaitingOnServer) }

    func reset() {
        runs = [:]
    }
}
