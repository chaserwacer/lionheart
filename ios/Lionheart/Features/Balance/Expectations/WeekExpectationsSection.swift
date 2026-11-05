import SwiftUI

/// The week's expectations as Train shows them beside the score: the adjusted score with the days it
/// was read against, each measurement's done against its adjusted target with its reasons, and each
/// priority lift's next top set.
///
/// The server's one answer, drawn: every target, raw and adjusted, and every reason comes from the
/// score's `expectations`, so this section, the volume rows and the chat cannot disagree. Absent from a
/// server that predates the expectations.
struct WeekExpectationsSection: View {
    let score: TrainingScore
    /// Opens the non-training days, where the days the targets prorate to are marked.
    let onEditDays: () -> Void

    @State private var isInfoShown = false

    var body: some View {
        if let expectations = score.expectations, let adjusted = score.adjusted {
            Section {
                AdjustedScoreRow(score: score, adjusted: adjusted, availability: .of(expectations.availability))
                ForEach(ExpectationLines.of(expectations)) { line in
                    ExpectationRow(line: line)
                }
                ForEach(expectations.priorities) { priority in
                    PriorityAimRow(priority: priority)
                }
                Button("Non-training days", systemImage: "calendar", action: onEditDays)
            } header: {
                HStack {
                    Text("This week's expectations")
                    Spacer()
                    Button("How the expectations are worked out", systemImage: "info.circle") { isInfoShown = true }
                        .labelStyle(.iconOnly)
                }
            }
            .sheet(isPresented: $isInfoShown) {
                ExpectationsInfoSheet()
            }
        }
    }
}

/// The adjusted score, and the days it was read against.
private struct AdjustedScoreRow: View {
    let score: TrainingScore
    let adjusted: AdjustedScore
    let availability: WeekAvailability

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    var body: some View {
        let layout = dynamicTypeSize.isAccessibilitySize
            ? AnyLayout(VStackLayout(alignment: .leading, spacing: 2))
            : AnyLayout(HStackLayout(alignment: .firstTextBaseline, spacing: 12))
        layout {
            VStack(alignment: .leading, spacing: 2) {
                Text("Adjusted score")
                    .font(.subheadline.weight(.semibold))
                Text(days)
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
            }
            if !dynamicTypeSize.isAccessibilitySize { Spacer(minLength: 8) }
            Text("\(adjusted.value) of \(adjusted.outOf)")
                .font(.headline.monospacedDigit())
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Adjusted score")
        .accessibilityValue("\(adjusted.value) of \(adjusted.outOf). \(days)")
    }

    private var days: String {
        switch availability {
        case .everyDay where adjusted.value == score.value && adjusted.outOf == score.outOf:
            "Every day available: the same as the score."
        case .everyDay:
            "Every day this week available; days were marked off in the weeks before."
        case .short(let available, let of, let off, let notes):
            "\(available) of \(of) days available · \(off.map(Self.weekday).joined(separator: ", ")) marked off"
                + (notes.isEmpty ? "" : " (\(notes.joined(separator: ", ")))")
        }
    }

    private static func weekday(_ day: CalendarDay) -> String {
        day.middayDate().formatted(.dateTime.weekday(.abbreviated))
    }
}

/// One measurement: done against the adjusted target, the raw one beside it when the days lowered it,
/// and the reasons under a disclosure.
private struct ExpectationRow: View {
    let line: ExpectationLine

    @State private var isExpanded = false
    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    var body: some View {
        DisclosureGroup(isExpanded: $isExpanded) {
            ForEach(line.expectation.reasons, id: \.self) { reason in
                Text(reason)
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        } label: {
            LabeledContent {
                // At the accessibility sizes the figure wraps under the name, where it reads from the left.
                VStack(alignment: dynamicTypeSize.isAccessibilitySize ? .leading : .trailing, spacing: 2) {
                    Text(figure)
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(Color(.label))
                    if line.expectation.isLowered {
                        Text("\(MeasurementText.plain(line.expectation.raw.target)) with every day")
                            .font(.caption.monospacedDigit())
                    }
                }
            } label: {
                HStack(spacing: 6) {
                    Text(title)
                    if line.isMet {
                        Image(systemName: "checkmark.circle.fill")
                            .foregroundStyle(.green)
                            .accessibilityLabel("Met")
                    }
                }
            }
            .accessibilityElement(children: .combine)
            .accessibilityHint(line.expectation.reasons.isEmpty ? "" : "Shows the reasons")
        }
    }

    private var title: String {
        switch line.measure {
        case .sessions: "Sessions"
        case .hardSets(let family): family.name
        case .minutes(let band): band.name
        }
    }

    private var unit: String {
        switch line.measure {
        case .sessions: ""
        case .hardSets: " sets"
        case .minutes: " min"
        }
    }

    private var figure: String {
        guard line.expectation.asksAnything else { return "nothing asked" }
        return "\(MeasurementText.plain(line.expectation.done)) of \(MeasurementText.plain(line.expectation.adjusted.target))\(unit)"
    }
}

/// A priority lift's next top set, how often the week asks for it, and why.
private struct PriorityAimRow: View {
    let priority: PriorityAim

    @State private var isExpanded = false

    var body: some View {
        DisclosureGroup(isExpanded: $isExpanded) {
            ForEach(priority.reasons, id: \.self) { reason in
                Text(reason)
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        } label: {
            VStack(alignment: .leading, spacing: 2) {
                Text(priority.name)
                Text([ExpectationWords.aim(priority.aim), frequency].compactMap { $0 }.joined(separator: " · "))
                    .font(.caption.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
            .accessibilityElement(children: .combine)
            .accessibilityHint(priority.reasons.isEmpty ? "" : "Shows the reasons")
        }
    }

    private var frequency: String? {
        switch priority.frequency {
        case .pinned(let sessions):
            "\(MeasurementText.plain(sessions.done)) of \(MeasurementText.plain(sessions.adjusted.target)) "
                + (sessions.adjusted.target == 1 ? "session" : "sessions") + " this week"
        case .notPinned(let sessions):
            sessions == 1 ? "1 session this week" : "\(sessions) sessions this week"
        case .unrecognised:
            nil
        }
    }
}
