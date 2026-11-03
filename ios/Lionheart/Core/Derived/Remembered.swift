import Foundation

/// The answer to a read the device remembers: the server's, just now, or the last one it gave when
/// it cannot be reached now.
///
/// A case rather than a value with an optional date beside it, because the two are different
/// things to show. A current answer is the screen; a recalled one is the screen as it stood at a
/// time the athlete has to be told, since a readiness from yesterday evening read as this morning's
/// is worse than no readiness at all.
enum Remembered<Value: Sendable>: Sendable {
    case current(Value)
    /// The server could not be reached. This is the answer it last gave, at `asOf`.
    case recalled(Value, asOf: Date)

    var value: Value {
        switch self {
        case .current(let value): value
        case .recalled(let value, _): value
        }
    }

    /// When a recalled answer dates from, or `nil` for a current one.
    var asOf: Date? {
        switch self {
        case .current: nil
        case .recalled(_, let asOf): asOf
        }
    }

    func map<Other: Sendable>(_ transform: (Value) throws -> Other) rethrows -> Remembered<Other> {
        switch self {
        case .current(let value): .current(try transform(value))
        case .recalled(let value, let asOf): .recalled(try transform(value), asOf: asOf)
        }
    }

    /// A value derived from several remembered reads, current only when every one of them was.
    ///
    /// A screen built from parts is as old as its oldest part, so that is the time it is marked
    /// with. One part current and another recalled is possible — a connection that dropped between
    /// two requests — and the screen is still not current.
    static func derived(_ value: Value, from parts: [Date?]) -> Remembered<Value> {
        guard let oldest = parts.compactMap({ $0 }).min() else { return .current(value) }
        return .recalled(value, asOf: oldest)
    }
}

extension Remembered: Equatable where Value: Equatable {}
