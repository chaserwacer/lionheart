import SwiftUI

/// A section header naming a movement, which opens the movement's page.
///
/// A link in the header rather than a row of its own, so the sets under it stay the section's only
/// rows.
struct MovementHeader: View {
    let movementDataId: UUID
    let name: String

    var body: some View {
        NavigationLink(value: HistoryRoute.movement(movementDataId)) {
            HeaderLinkLabel(Text(name))
        }
        .accessibilityHint("Opens the movement's history")
    }
}
