import Foundation

/// One server-sent event: its name and its data.
struct ServerSentEvent: Equatable, Sendable {
    let name: String
    let data: String
}

/// Reads server-sent events out of a response's lines.
///
/// Only the framing Lionheart's server writes: an `event:` line naming the event, then one `data:`
/// line of JSON, which is what dispatches it. That is deliberate rather than a shortcut. A blank
/// line ends an event in the full format, but `URLSession.AsyncBytes.lines` does not hand blank
/// lines on, so a reader that waited for one would never dispatch anything. A comment line — the
/// server's heartbeat — and any field this app does not use are passed over.
struct ServerSentEventReader: Sendable {
    private var name = "message"

    /// The event `line` completes, if it completes one.
    mutating func read(_ line: String) -> ServerSentEvent? {
        if line.hasPrefix(":") { return nil }
        if let value = Self.value(of: "event", in: line) {
            name = value
            return nil
        }
        guard let data = Self.value(of: "data", in: line) else { return nil }
        defer { name = "message" }
        return ServerSentEvent(name: name, data: data)
    }

    /// The value of a `field: value` line, the one space after the colon dropped as the format says.
    private static func value(of field: String, in line: String) -> String? {
        guard line.hasPrefix(field + ":") else { return nil }
        let value = line.dropFirst(field.count + 1)
        return String(value.hasPrefix(" ") ? value.dropFirst() : value)
    }
}

/// The events in a stream of lines, in order.
struct ServerSentEvents<Lines: AsyncSequence>: AsyncSequence where Lines.Element == String {
    typealias Element = ServerSentEvent

    let lines: Lines

    struct AsyncIterator: AsyncIteratorProtocol {
        var lines: Lines.AsyncIterator
        var reader = ServerSentEventReader()

        mutating func next() async throws -> ServerSentEvent? {
            while let line = try await lines.next() {
                if let event = reader.read(line) { return event }
            }
            return nil
        }
    }

    func makeAsyncIterator() -> AsyncIterator {
        AsyncIterator(lines: lines.makeAsyncIterator())
    }
}
