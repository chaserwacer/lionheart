import SwiftUI

/// One set as one tabular line: its number, the load, the work done against it, its RPE, and
/// whether it beat a record.
///
/// A custom view because the row is read *down the column* — where the load went, where the reps
/// fell off, where the effort climbed — and no stock row aligns figures across rows. Each column has
/// a width scaled with Dynamic Type, so the figures line up through `.xxLarge`. Above it the
/// columns would no longer fit a phone's row, so the figures stack rather than truncate — decided by the
/// size rather than by measuring each row, because a row without an RPE would otherwise fit as
/// columns while its neighbours stacked, and a column that holds for some rows is no column.
///
/// The density comes from the layout, not the type: the figures stay at `.body`, the row's vertical
/// insets are tightened to what a single line needs, and a set that met its prescription does not
/// repeat it underneath.
struct SetRow: View {
    let number: Int
    let figures: SetFigures
    /// The prescription the set answered, only where the set fell short of it.
    let unmetTarget: SetPrescription?
    let record: SetRecordOutcome

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize
    @ScaledMetric(relativeTo: .subheadline) private var numberWidth: CGFloat = 20
    @ScaledMetric(relativeTo: .body) private var amountWidth: CGFloat = 78
    @ScaledMetric(relativeTo: .body) private var workWidth: CGFloat = 52
    @ScaledMetric(relativeTo: .subheadline) private var ratingWidth: CGFloat = 56
    @ScaledMetric(relativeTo: .caption) private var markWidth: CGFloat = 16

    /// A set on the logging surface, measured against its target and the standing records.
    init(number: Int, command: SetCommand, target: SetPrescription?, record: SetRecordOutcome) {
        self.number = number
        self.figures = .of(command)
        self.unmetTarget = target.flatMap { $0.isMet(by: command) ? nil : $0 }
        self.record = record
    }

    /// A set in a finished session, read back from the server.
    init(number: Int, entry: SetEntry) {
        self.number = number
        self.figures = .of(entry)
        self.unmetTarget = nil
        self.record = .notApplicable
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            if dynamicTypeSize > .xxLarge {
                stacked
            } else {
                columns
            }
            if let unmetTarget {
                Text("Target \(Formatting.target(unmetTarget))")
                    .font(.footnote.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
                    .padding(.leading, numberWidth + Self.spacing)
            }
        }
        .listRowInsets(EdgeInsets(top: 7, leading: 20, bottom: 7, trailing: 20))
        .accessibilityElement(children: .combine)
        .accessibilityLabel(accessibilityDescription)
    }

    private static let spacing: CGFloat = 8

    // MARK: - Layouts

    /// The figures in aligned columns: number, load, work, then RPE and the record mark at the
    /// trailing edge, each a fixed width so a row without one keeps the others in place.
    private var columns: some View {
        HStack(alignment: .firstTextBaseline, spacing: Self.spacing) {
            numberText
            switch figures {
            case .measured(let measured):
                amountText(measured)
                    .frame(minWidth: amountWidth, alignment: .trailing)
                Text(measured.work)
                    .font(.body.monospacedDigit())
                    .foregroundStyle(Color(.label))
                    .frame(minWidth: workWidth, alignment: .leading)
                if let extra = measured.extra {
                    Text(extra)
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                }
                Spacer(minLength: 0)
                ratingText(measured.rpe)
                    .frame(minWidth: ratingWidth, alignment: .trailing)
            case .described, .undescribed:
                words
                Spacer(minLength: 0)
            }
            recordMark
                .frame(width: markWidth)
        }
        .lineLimit(1)
    }

    /// The same figures, one under another, for the text sizes above `.xxLarge`.
    private var stacked: some View {
        HStack(alignment: .firstTextBaseline, spacing: Self.spacing) {
            numberText
            VStack(alignment: .leading, spacing: 2) {
                switch figures {
                case .measured(let measured):
                    Text("\(amountText(measured)) \(measured.work)")
                        .foregroundStyle(Color(.label))
                    if let extra = measured.extra {
                        Text(extra)
                            .font(.subheadline.monospacedDigit())
                            .foregroundStyle(Color(.secondaryLabel))
                    }
                    ratingText(measured.rpe)
                case .described, .undescribed:
                    words
                }
            }
            Spacer(minLength: 0)
            recordMark
        }
    }

    // MARK: - Pieces

    private var numberText: some View {
        Text("\(number)")
            .font(.subheadline.monospacedDigit())
            .foregroundStyle(Color(.secondaryLabel))
            .frame(minWidth: numberWidth, alignment: .trailing)
    }

    /// The load in the body figure, its unit quieter beside it: the number is what is scanned.
    private func amountText(_ measured: SetFigures.Measured) -> Text {
        Text("\(Text(measured.amount).foregroundStyle(Color(.label))) \(Text(measured.unit).foregroundStyle(Color(.secondaryLabel)))")
            .font(.body.monospacedDigit())
    }

    @ViewBuilder
    private func ratingText(_ rpe: Double?) -> some View {
        if let rpe {
            Text("\(Text("RPE").foregroundStyle(Color(.tertiaryLabel))) \(Formatting.measure(rpe))")
                .font(.subheadline.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
        }
    }

    /// A set whose words are the record: the words, or a quieter note that there are none.
    @ViewBuilder
    private var words: some View {
        switch figures {
        case .described(let text):
            Text(text)
                .font(.body)
                .foregroundStyle(Color(.label))
                .lineLimit(3)
        case .undescribed:
            Text("Logged without detail")
                .font(.body)
                .foregroundStyle(Color(.secondaryLabel))
        case .measured:
            EmptyView()
        }
    }

    /// A status glyph, not decoration: it is the only place on the row that says the set beat a
    /// standing record, and VoiceOver reads what it beat.
    @ViewBuilder
    private var recordMark: some View {
        if Formatting.recordsBeaten(record) != nil {
            Image(systemName: "trophy.fill")
                .font(.caption)
                .foregroundStyle(.orange)
        }
    }

    /// Read out as a sentence, because the visual row leans on column position for the set number
    /// and on a glyph for the record — neither of which survives being spoken.
    private var accessibilityDescription: String {
        var parts = ["Set \(number)"]
        switch figures {
        case .measured(let measured):
            parts.append(measured.spoken)
            if let rpe = measured.rpe { parts.append(Formatting.rpe(rpe)) }
        case .described(let text):
            parts.append(text)
        case .undescribed:
            parts.append("logged without detail")
        }
        if let unmetTarget { parts.append("target \(Formatting.target(unmetTarget))") }
        if let beaten = Formatting.recordsBeaten(record) { parts.append(beaten) }
        return parts.joined(separator: ", ")
    }
}
