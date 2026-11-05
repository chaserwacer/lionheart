import SwiftUI

/// What to train next in one line — "Legs then 60 min zone 2" — with the session to do first in
/// Brick, the one thing on a hero to act on, and the other after it.
///
/// The server's recommendation in as few words as it goes: Today's and Train's heroes carry it, and
/// the reasons and the lifts' aims stay with the up-next section that states them.
struct NextUpLine: View {
    let next: NextSessions

    var body: some View {
        VStack(alignment: .leading, spacing: 1) {
            Text("Up next")
                .font(.meta)
                .foregroundStyle(Color(.secondaryLabel))
            Group {
                if let second {
                    Text("\(Text(first).foregroundStyle(Tokens.accent)) then \(second)")
                } else {
                    Text(first).foregroundStyle(Tokens.accent)
                }
            }
            .font(.headline)
        }
        .padding(.vertical, 1)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Up next")
        .accessibilityValue(second.map { "\(first) first, then \($0)" } ?? first)
    }

    private var liftFirst: Bool {
        switch next.first.session {
        case .lift, .unrecognised: true
        case .cardio: false
        }
    }

    private var first: String {
        liftFirst ? lift : (cardio?.capitalisingFirst ?? lift)
    }

    private var second: String? {
        guard let cardio else { return nil }
        return liftFirst ? cardio : lift.lowercased()
    }

    private var lift: String {
        switch next.lift {
        case .train(let families, _, _, _) where !families.isEmpty:
            PatternFamily.listed(families).capitalisingFirst
        case .train, .nothingLogged, .unrecognised:
            "A lift"
        }
    }

    private var cardio: String? { GoingInWords.cardio(next.cardio) }
}
