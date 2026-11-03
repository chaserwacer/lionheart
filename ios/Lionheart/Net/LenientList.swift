import Foundation

/// A top-level JSON array whose unreadable elements are dropped rather than failing the whole list.
///
/// The same reasoning as `lenientList`, which only works inside an object: one row this client
/// cannot read must not be able to empty a screen. A row carrying an unfamiliar `$type` is not
/// affected — the hierarchies in `Core/Model/` all decode that to their own unrecognised case, so
/// an element only fails here when the payload is genuinely malformed.
struct LenientList<Element: Decodable>: Decodable {
    let elements: [Element]

    init(from decoder: any Decoder) throws {
        var container = try decoder.unkeyedContainer()
        var elements: [Element] = []

        while !container.isAtEnd {
            if let element = try? container.decode(Element.self) {
                elements.append(element)
            } else if (try? container.decode(SkippedElement.self)) == nil {
                // Neither read moved the cursor, so continuing would spin. Stop with what we have.
                break
            }
        }
        self.elements = elements
    }
}
