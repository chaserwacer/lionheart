import SwiftUI

/// What a section header that opens something reads as: its words in the label colour, with the
/// platform's chevron after them.
///
/// Not the accent: a session's detail has a header per movement and a movement's history one per
/// session, and a column of tinted headers would be the loudest thing on the screen. The chevron is
/// what says the header goes somewhere.
struct HeaderLinkLabel<Trailing: View>: View {
    let title: Text
    let trailing: Trailing

    init(_ title: Text, @ViewBuilder trailing: () -> Trailing = { EmptyView() }) {
        self.title = title
        self.trailing = trailing()
    }

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: 4) {
            title.foregroundStyle(Color(.label))
            Image(systemName: "chevron.forward")
                .font(.caption.weight(.semibold))
                .imageScale(.small)
                .foregroundStyle(Color(.tertiaryLabel))
                .accessibilityHidden(true)
            Spacer(minLength: 8)
            trailing.foregroundStyle(Color(.secondaryLabel))
        }
    }
}

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
