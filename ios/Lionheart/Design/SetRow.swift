import SwiftUI

/// One logged set, as a row in the workout being logged.
///
/// A custom view because a set is four facts at three weights of emphasis — its number, what it
/// did, what it was asked to do, and whether it beat anything — and no stock row shapes that.
/// `LabeledContent` comes close and puts the label on the left, which is wrong here: the numbers
/// are what the athlete scans down the column for.
struct SetRow: View {
    let number: Int
    let command: SetCommand
    let target: SetPrescription?
    let record: SetRecordOutcome

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: Tokens.cardPadding) {
            Text("\(number)")
                .font(.subheadline.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
                .frame(minWidth: 18, alignment: .trailing)
                .accessibilityHidden(true)

            VStack(alignment: .leading, spacing: 2) {
                if let numbers = Formatting.set(command) {
                    Text(numbers)
                        .font(.body.monospacedDigit())
                        .foregroundStyle(Color(.label))
                } else if case .freeform(let text) = command {
                    Text(text.isEmpty ? "Logged without detail" : text)
                        .font(.body)
                        .foregroundStyle(text.isEmpty ? Color(.secondaryLabel) : Color(.label))
                }

                if let target {
                    Text("Target \(Formatting.target(target))")
                        .font(.caption)
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }

            Spacer(minLength: 4)

            if let value = command.rpe.value {
                Text(Formatting.measure(value))
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
                    .accessibilityLabel(Formatting.rpe(value))
            }

            if record.isWorthSaying {
                Image(systemName: "trophy.fill")
                    .font(.caption)
                    .foregroundStyle(.orange)
                    .accessibilityLabel(Self.recordLabel(record))
            }
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel(accessibilityDescription)
    }

    /// Read out as a sentence, because the visual row leans on column position for the set number
    /// and on an icon for the record — neither of which survives being spoken.
    private var accessibilityDescription: String {
        var parts = ["Set \(number)"]
        if let numbers = Formatting.set(command) {
            parts.append(numbers)
        } else if case .freeform(let text) = command, !text.isEmpty {
            parts.append(text)
        }
        if let value = command.rpe.value { parts.append(Formatting.rpe(value)) }
        if let target { parts.append("target \(Formatting.target(target))") }
        if record.isWorthSaying { parts.append(Self.recordLabel(record)) }
        return parts.joined(separator: ", ")
    }

    private static func recordLabel(_ record: SetRecordOutcome) -> String {
        switch record {
        case .strength: "Heaviest yet"
        case .volume: "Best volume yet"
        case .both: "Heaviest and best volume yet"
        case .notApplicable, .noRecordYet, .none: ""
        }
    }
}
