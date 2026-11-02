import Foundation

/// An arbitrary JSON value, decoded and re-encoded without being understood.
///
/// It exists for exactly one job: the interpretation engine's resume path. The client receives a
/// parsed draft, shows the athlete which names were ambiguous, and echoes **the same draft** back
/// to `POST /confirm` so the server re-resolves without re-parsing (and re-paying for) the text.
/// Decoding that draft into Swift values would make every field of the parser's output a shape this
/// client has to mirror exactly, and any member it dropped would silently change what the server
/// re-resolves. Carrying the value as-is removes that whole class of bug.
///
/// Re-encoding may order an object's keys differently from how they arrived. That is safe here
/// because the server sets `AllowOutOfOrderMetadataProperties`, so a `$type` that is no longer the
/// first property still reads — without that setting this approach would not work.
enum JSONValue: Equatable, Sendable {
    case null
    case bool(Bool)
    case number(Double)
    case string(String)
    case array([JSONValue])
    case object([String: JSONValue])
}

extension JSONValue: Codable {
    init(from decoder: any Decoder) throws {
        if let container = try? decoder.container(keyedBy: WireKey.self) {
            var object: [String: JSONValue] = [:]
            for key in container.allKeys {
                object[key.stringValue] = try container.decode(JSONValue.self, forKey: key)
            }
            self = .object(object)
            return
        }

        if var container = try? decoder.unkeyedContainer() {
            var array: [JSONValue] = []
            while !container.isAtEnd {
                array.append(try container.decode(JSONValue.self))
            }
            self = .array(array)
            return
        }

        let scalar = try decoder.singleValueContainer()
        if scalar.decodeNil() {
            self = .null
        } else if let bool = try? scalar.decode(Bool.self) {
            self = .bool(bool)
        } else if let number = try? scalar.decode(Double.self) {
            self = .number(number)
        } else {
            self = .string(try scalar.decode(String.self))
        }
    }

    func encode(to encoder: any Encoder) throws {
        switch self {
        case .null:
            var scalar = encoder.singleValueContainer()
            try scalar.encodeNil()
        case .bool(let bool):
            var scalar = encoder.singleValueContainer()
            try scalar.encode(bool)
        case .number(let number):
            var scalar = encoder.singleValueContainer()
            // An integral value is written without a decimal point, so an int-typed member on the
            // server still binds when the draft comes back.
            if number == number.rounded(), abs(number) < 9_007_199_254_740_992 {
                try scalar.encode(Int(number))
            } else {
                try scalar.encode(number)
            }
        case .string(let string):
            var scalar = encoder.singleValueContainer()
            try scalar.encode(string)
        case .array(let array):
            var container = encoder.unkeyedContainer()
            for element in array { try container.encode(element) }
        case .object(let object):
            var container = encoder.container(keyedBy: WireKey.self)
            for (key, value) in object {
                try container.encode(value, forKey: WireKey(stringValue: key))
            }
        }
    }
}
