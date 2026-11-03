import Foundation

/// Waiting for something, but not for ever.
enum Deadline {
    /// Runs `operation` and answers its result, or throws `expiry` once `limit` has passed without
    /// one. The operation is cancelled when the limit wins, and the limit when the operation does.
    ///
    /// Structured rather than a timer beside a task: both halves are children of one group, so
    /// cancelling the caller reaches both and neither can outlive it.
    static func waiting<T: Sendable>(
        atMost limit: Duration,
        expiring expiry: @escaping @Sendable () -> any Error,
        for operation: @escaping @Sendable () async throws -> T
    ) async throws -> T {
        try await withThrowingTaskGroup(of: T.self) { group in
            group.addTask(operation: operation)
            group.addTask {
                try await Task.sleep(for: limit)
                throw expiry()
            }
            defer { group.cancelAll() }
            guard let first = try await group.next() else { throw CancellationError() }
            return first
        }
    }
}
