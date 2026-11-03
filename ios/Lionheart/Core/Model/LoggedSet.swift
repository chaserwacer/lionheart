import Foundation

/// One set the athlete has logged in the workout in flight.
///
/// It is a `SetCommand` plus two facts the server neither holds nor needs. The **id** is the
/// device's own: the server stores sets as one ordered JSON list with no per-set identity, so
/// editing or deleting the third set of five by index would drift the moment a set was inserted,
/// and a stable id is what lets a row be addressed instead of counted. The **instant** is what
/// makes a rest clock recoverable after a force-quit, and it gives each row a time without a
/// round trip.
///
/// Neither is sent. `PUT /{id}/sets` carries the `command` values alone, in order.
struct LoggedSet: Equatable, Sendable, Identifiable, Codable {
    let id: UUID
    let loggedAt: Date
    let command: SetCommand

    init(id: UUID = UUID(), loggedAt: Date, command: SetCommand) {
        self.id = id
        self.loggedAt = loggedAt
        self.command = command
    }

    var movementDataId: UUID? { command.movementDataId }
    var modality: SetModality { command.modality }

    /// The same set with different numbers, keeping its identity and the moment it was logged.
    ///
    /// Correcting a typed weight does not make it a new set, and re-stamping the time would move a
    /// rest that has already been and gone.
    func restated(as command: SetCommand) -> LoggedSet {
        LoggedSet(id: id, loggedAt: loggedAt, command: command)
    }
}
