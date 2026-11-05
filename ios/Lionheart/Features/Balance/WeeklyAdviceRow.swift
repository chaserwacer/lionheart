import SwiftUI

/// The week's one piece of advice as a hero row: "This week" over the headline, the server's reasons
/// folded under a disclosure.
///
/// It sits in Today's and Train's heroes for the whole week, so its headline is the one thing on the
/// hero besides the session to do first that is drawn in Brick: Brick means this week. The reasons are
/// the server's words, the same ones the chat is given, so what the banner says and what the chat
/// says agree.
struct WeeklyAdviceRow: View {
    let advice: WeeklyAdvice

    @State private var isExpanded = false

    var body: some View {
        DisclosureGroup(isExpanded: $isExpanded) {
            ForEach(advice.reasons, id: \.self) { reason in
                Text(reason)
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
                    .fixedSize(horizontal: false, vertical: true)
            }
        } label: {
            Label {
                VStack(alignment: .leading, spacing: 1) {
                    Text("This week")
                        .font(.meta)
                        .foregroundStyle(Color(.secondaryLabel))
                    Text(advice.headline)
                        .font(.headline)
                        .foregroundStyle(Tokens.accent)
                        .fixedSize(horizontal: false, vertical: true)
                }
            } icon: {
                Image(systemName: symbol)
                    .foregroundStyle(Tokens.accent)
            }
            .accessibilityElement(children: .ignore)
            .accessibilityLabel("This week's advice")
            .accessibilityValue(advice.headline)
            .accessibilityHint(advice.reasons.isEmpty ? "" : "Shows why")
        }
        .tint(Color(.secondaryLabel))
    }

    private var symbol: String {
        switch advice.kind {
        case .manageInjury: "bandage"
        case .takeDeload: "arrow.down.right"
        case .easeRamp: "gauge.with.dots.needle.67percent"
        case .easeAfterEverydayLoad: "figure.walk"
        case .emphasiseCardio: "heart"
        case .emphasiseLifting: "dumbbell"
        case .priorityStalled: "arrow.triangle.2.circlepath"
        case .keepBuilding: "chart.line.uptrend.xyaxis"
        case .unrecognised: "lightbulb"
        }
    }
}
