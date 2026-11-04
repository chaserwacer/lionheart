import Foundation

/// One line of text a recogniser found on a page, and where on the page it sits.
///
/// The position is normalised to the page with the origin at the top left, so a line further down
/// the sheet has a larger `y` — the way the sheet is read, and the opposite of Vision's own space.
struct RecognizedLine: Equatable, Sendable, Codable {
    let text: String
    let x: Double
    let y: Double
    let width: Double
    let height: Double

    enum CodingKeys: String, CodingKey {
        case text, x, y
        case width = "w"
        case height = "h"
    }

    var midY: Double { y + height / 2 }
    var maxX: Double { x + width }
}
