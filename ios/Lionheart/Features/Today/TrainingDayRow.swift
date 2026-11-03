import SwiftUI

/// One session on a day, as a log-book entry: what it came to, then each movement on its own line —
/// `Barbell Squat   330 lb × 6, 5, 5, 4`.
///
/// The movements are a two-column grid, so the loads line up down the right edge and a session of
/// seven movements is still seven short lines rather than a paragraph. At the accessibility sizes
/// each movement's line moves under its name instead of being squeezed beside it.
struct TrainingDayRow: View {
    let session: TrainingSession
    let catalogue: MovementCatalogue
    /// Shown above the session when the row is not about the day on screen — "Last session" on a
    /// day with nothing logged.
    var dayLabel: String?

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    var body: some View {
        let headingLayout = dynamicTypeSize.isAccessibilitySize
            ? AnyLayout(VStackLayout(alignment: .leading, spacing: 2))
            : AnyLayout(HStackLayout(alignment: .firstTextBaseline))

        VStack(alignment: .leading, spacing: 6) {
            headingLayout {
                Text(heading)
                    .font(.subheadline.weight(.semibold))
                if !dynamicTypeSize.isAccessibilitySize { Spacer(minLength: 8) }
                if let facts {
                    Text(facts)
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }

            let digests = MovementDigest.of(session.sets, catalogue: catalogue)
            if !digests.isEmpty {
                if dynamicTypeSize.isAccessibilitySize {
                    VStack(alignment: .leading, spacing: 4) {
                        ForEach(digests) { digest in
                            VStack(alignment: .leading, spacing: 0) {
                                Text(digest.movementName).font(.subheadline)
                                Text(Self.line(digest.line))
                                    .font(.subheadline.monospacedDigit())
                                    .foregroundStyle(Color(.secondaryLabel))
                            }
                        }
                    }
                } else {
                    Grid(alignment: .leadingFirstTextBaseline, horizontalSpacing: 12, verticalSpacing: 3) {
                        ForEach(digests) { digest in
                            GridRow {
                                // The name wraps before a figure truncates: the loads are the
                                // reason the row is read.
                                Text(digest.movementName)
                                    .font(.subheadline)
                                    .lineLimit(2)
                                    .fixedSize(horizontal: false, vertical: true)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                                Text(Self.line(digest.line))
                                    .font(.subheadline.monospacedDigit())
                                    .foregroundStyle(Color(.secondaryLabel))
                                    .fixedSize()
                                    .gridColumnAlignment(.trailing)
                            }
                        }
                    }
                }
            }

            if let notes = session.envelope.notes.nonEmpty {
                Text(notes)
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
                    .lineLimit(3)
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }

    /// What leads the row: the day when the row is not about the day on screen, the time a
    /// completed session started, or the state of any other.
    private var heading: String {
        if let dayLabel { return dayLabel }
        if case .completed(let completed) = session, let start = completed.period.startTime {
            return start.formatted(date: .omitted, time: .shortened)
        }
        return SessionLog.kind(of: session, today: CalendarDay(Date())).label
    }

    private var facts: String? {
        Formatting.sessionFacts(SessionLog.facts(of: session))
    }

    /// A movement's line as a lifter writes it.
    static func line(_ line: MovementDigest.Line) -> String {
        switch line {
        case .straight(let weight, let unit, let reps):
            let load = Formatting.weight(weight, unit)
            return "\(load) × \(reps.map(String.init).joined(separator: ", "))"
        case .ramped(let top, let setCount):
            let load = Formatting.weight(top.weight, top.unit)
            return "\(Formatting.count(setCount, "set")), top \(load) × \(top.reps)"
        case .counted(let setCount):
            return Formatting.count(setCount, "set")
        }
    }
}
