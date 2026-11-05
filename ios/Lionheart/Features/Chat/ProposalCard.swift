import SwiftUI

/// A change Tempo proposed, in the transcript where it proposed it: what it will change, and the
/// athlete's answer.
///
/// A card rather than a bubble, because it is not something said — it is the one place in the
/// thread where a tap writes to his data, so it says exactly what will change before he confirms,
/// and afterwards it says what became of it. Nothing is written until he taps Confirm; the server
/// renders every line from the change itself, so the card cannot say one thing and do another.
struct ProposalCard: View {
    let proposal: ChangeProposal
    /// Whether this card's answer is out.
    let isAnswering: Bool
    /// Whether another card's answer is out, which holds this card's buttons.
    let isHeld: Bool
    let onConfirm: () -> Void
    let onDecline: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Label {
                Text(proposal.title).font(.headline)
            } icon: {
                Image(systemName: symbol)
                    .foregroundStyle(Tokens.accent)
            }

            if !proposal.lines.isEmpty {
                VStack(alignment: .leading, spacing: 4) {
                    ForEach(Array(proposal.lines.enumerated()), id: \.offset) { _, line in
                        Text(line)
                            .font(.subheadline)
                            .foregroundStyle(Color(.secondaryLabel))
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
            }

            Divider()
            footer
        }
        .padding(Tokens.cardPadding)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Color(.secondarySystemGroupedBackground), in: .rect(cornerRadius: 18))
        .overlay {
            RoundedRectangle(cornerRadius: 18)
                .strokeBorder(proposal.answer.isWaiting ? Tokens.accent.opacity(0.5) : Color(.separator), lineWidth: 1)
        }
        .sensoryFeedback(.success, trigger: isConfirmed)
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Proposed change: \(proposal.title)")
    }

    @ViewBuilder
    private var footer: some View {
        switch proposal.answer {
        case .waiting:
            HStack(spacing: 12) {
                Text("Nothing changes until you confirm.")
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
                    .frame(maxWidth: .infinity, alignment: .leading)
                if isAnswering {
                    ProgressView()
                } else {
                    Button("Decline", action: onDecline)
                        .buttonStyle(.bordered)
                    Button("Confirm", action: onConfirm)
                        .buttonStyle(.borderedProminent)
                }
            }
            .controlSize(.small)
            .disabled(isHeld)
        case .confirmed:
            Label("Confirmed", systemImage: "checkmark.circle.fill")
                .font(.footnote.weight(.semibold))
                .foregroundStyle(.green)
        case .declined:
            Label("Declined — nothing changed", systemImage: "xmark.circle")
                .font(.footnote)
                .foregroundStyle(Color(.secondaryLabel))
        case .refused(_, let reason):
            Label("Not saved: \(reason)", systemImage: "exclamationmark.triangle.fill")
                .font(.footnote)
                .foregroundStyle(.orange)
        case .unrecognised:
            Label("Answered", systemImage: "circle")
                .font(.footnote)
                .foregroundStyle(Color(.secondaryLabel))
        }
    }

    private var isConfirmed: Bool {
        if case .confirmed = proposal.answer { true } else { false }
    }

    private var symbol: String {
        switch proposal.area {
        case .plan: "calendar.badge.plus"
        case .availability: "calendar.badge.minus"
        case .focus: "pin"
        case .injuries: "bandage"
        case .unrecognised: "square.and.pencil"
        }
    }
}
