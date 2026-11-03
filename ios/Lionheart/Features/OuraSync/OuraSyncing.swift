import Foundation

/// What the Oura sync needs from the server: how far the store reaches, and the sync itself.
///
/// A seam at the network boundary for the same reason `WorkoutSessionWriting` is one: the thing
/// stood in for is the server, which a unit test cannot run, and the sync's interesting behaviour —
/// what it asks for, what it says when the server refuses — is all about the server's answers.
protocol OuraSyncing: Sendable {
    func coverage() async throws -> OuraCoverage
    func sync(_ range: DayRange) async throws -> OuraSyncReport
}

extension OuraAPI: OuraSyncing {}
